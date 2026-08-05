using Microsoft.EntityFrameworkCore;
using PosFlow.Application.Common;
using PosFlow.Application.Orders;
using PosFlow.Domain.Entities;
using PosFlow.Infrastructure.Persistence;

namespace PosFlow.Infrastructure.Orders;

public sealed class OrderService(
    PosFlowDbContext dbContext,
    ICurrentUser currentUser)
    : IOrderService
{
    private const int MaxOrderNumberAttempts = 3;

    private readonly PosFlowDbContext _dbContext = dbContext;
    private readonly ICurrentUser _currentUser = currentUser;

    public async Task<OrderResponse> CheckoutAsync(
        CreateOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        var shift = await GetOpenShiftAsync(cancellationToken);

        if (shift is null)
        {
            throw new InvalidOperationException(
                "لا توجد وردية مفتوحة. الرجاء فتح وردية أولاً.");
        }

        var productIds = request.Lines
            .Select(x => x.ProductId)
            .Distinct()
            .ToList();

        // Tracked (not AsNoTracking) because stock-tracked products get
        // their StockQuantity decremented and saved in this same
        // transaction as the order/lines/payments.
        var products = await _dbContext.Products
            .Where(
                x =>
                    x.TenantId == _currentUser.TenantId &&
                    productIds.Contains(x.Id))
            .ToDictionaryAsync(
                x => x.Id,
                cancellationToken);

        var hasMissingProduct = productIds
            .Any(id => !products.ContainsKey(id));

        if (hasMissingProduct)
        {
            throw new KeyNotFoundException(
                "أحد المنتجات المطلوبة غير موجود.");
        }

        var inactiveProduct = request.Lines
            .Select(line => products[line.ProductId])
            .FirstOrDefault(product => !product.IsActive);

        if (inactiveProduct is not null)
        {
            throw new InvalidOperationException(
                $"المنتج \"{inactiveProduct.NameAr}\" غير متاح حاليًا.");
        }

        // Aggregate quantities per product first, since the same
        // product could appear in more than one line.
        var quantityByProduct = request.Lines
            .GroupBy(x => x.ProductId)
            .ToDictionary(
                g => g.Key,
                g => g.Sum(x => x.Quantity));

        foreach (var (productId, requestedQuantity) in quantityByProduct)
        {
            var product = products[productId];

            if (product.TrackStock &&
                product.StockQuantity < requestedQuantity)
            {
                throw new InvalidOperationException(
                    $"الكمية المتاحة من \"{product.NameAr}\" غير كافية (المتاح: {product.StockQuantity}).");
            }
        }

        var orderLines = new List<OrderLine>();
        decimal subtotal = 0;
        decimal totalDiscount = 0;

        foreach (var lineRequest in request.Lines)
        {
            var product = products[lineRequest.ProductId];
            var lineGross = lineRequest.Quantity * product.Price;

            if (lineRequest.DiscountAmount > lineGross)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(lineRequest.DiscountAmount),
                    $"خصم الصنف \"{product.NameAr}\" أكبر من قيمته.");
            }

            orderLines.Add(new OrderLine
            {
                TenantId = _currentUser.TenantId,
                ProductId = product.Id,
                ProductName = product.NameAr,
                Quantity = lineRequest.Quantity,
                UnitPrice = product.Price,
                DiscountAmount = lineRequest.DiscountAmount,
                TaxAmount = 0,
                LineTotal = lineGross - lineRequest.DiscountAmount
            });

            subtotal += lineGross;
            totalDiscount += lineRequest.DiscountAmount;
        }

        // Flat tenant-configured VAT/sales tax rate (Tenant.TaxRatePercent,
        // 0 by default - matches the previous hardcoded no-tax
        // behaviour for tenants that haven't set one). Line-level
        // TaxAmount stays 0 for now (no per-product tax categories) -
        // only the order-level total carries the tax.
        var taxRatePercent = await _dbContext.Tenants
            .AsNoTracking()
            .Where(x => x.Id == _currentUser.TenantId)
            .Select(x => x.TaxRatePercent)
            .SingleOrDefaultAsync(cancellationToken);

        var taxableAmount = subtotal - totalDiscount;
        var taxAmount = Math.Round(
            taxableAmount * taxRatePercent / 100m,
            2,
            MidpointRounding.AwayFromZero);

        var totalAmount = taxableAmount + taxAmount;
        var paidAmount = request.Payments.Sum(x => x.Amount);

        if (paidAmount < totalAmount)
        {
            throw new InvalidOperationException(
                "المبلغ المدفوع أقل من إجمالي الفاتورة.");
        }

        Customer? customer = null;

        if (request.CustomerId.HasValue)
        {
            customer = await _dbContext.Customers
                .SingleOrDefaultAsync(
                    x =>
                        x.Id == request.CustomerId.Value &&
                        x.TenantId == _currentUser.TenantId,
                    cancellationToken);

            if (customer is null)
            {
                throw new KeyNotFoundException("العميل غير موجود.");
            }

            // 1 loyalty point per whole currency unit spent - simple
            // and tenant-agnostic; a points-per-currency-unit setting
            // can be added to Tenant later if shops want to tune it.
            customer.LoyaltyPoints += (int)Math.Floor(totalAmount);
            customer.UpdatedAtUtc = DateTime.UtcNow;
        }

        var payments = request.Payments
            .Select(paymentRequest => new Payment
            {
                TenantId = _currentUser.TenantId,
                Method = paymentRequest.Method,
                Amount = paymentRequest.Amount,
                ReferenceNumber = paymentRequest.ReferenceNumber
            })
            .ToList();

        var order = new Order
        {
            TenantId = _currentUser.TenantId,
            BranchId = _currentUser.BranchId,
            ShiftId = shift.Id,
            CustomerId = customer?.Id,
            Status = OrderStatus.Completed,
            Subtotal = subtotal,
            DiscountAmount = totalDiscount,
            TaxAmount = taxAmount,
            TotalAmount = totalAmount,
            Lines = orderLines,
            Payments = payments
        };

        // Decrement stock now - if the order-number retry loop below
        // has to retry, the same decrement is harmless to redo since
        // it's applied to the same tracked Product instances only once
        // (outside the loop), then saved together with the order.
        foreach (var (productId, requestedQuantity) in quantityByProduct)
        {
            var product = products[productId];

            if (product.TrackStock)
            {
                product.StockQuantity -= requestedQuantity;
                product.UpdatedAtUtc = DateTime.UtcNow;
            }
        }

        for (var attempt = 1; attempt <= MaxOrderNumberAttempts; attempt++)
        {
            order.OrderNumber =
                await GenerateOrderNumberAsync(cancellationToken);

            _dbContext.Orders.Add(order);

            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);

                return MapResponse(order, paidAmount);
            }
            catch (DbUpdateException) when (attempt < MaxOrderNumberAttempts)
            {
                // Two cashiers likely generated the same order number at
                // the same moment (the unique index on OrderNumber caught
                // it). Detach the order graph and retry with a fresh
                // number - the tracked Product stock changes stay
                // attached and are re-saved on the next attempt.
                DetachOrderGraph(order, orderLines, payments);
            }
        }

        throw new InvalidOperationException(
            "تعذر إنشاء رقم فاتورة فريد، حاول مرة أخرى.");
    }

    public async Task<OrderResponse?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var order = await _dbContext.Orders
            .AsNoTracking()
            .Include(x => x.Lines)
            .Include(x => x.Payments)
            .SingleOrDefaultAsync(
                x =>
                    x.Id == id &&
                    x.TenantId == _currentUser.TenantId,
                cancellationToken);

        return order is null
            ? null
            : MapResponse(order, order.Payments.Sum(x => x.Amount));
    }

    public async Task<PagedResult<OrderResponse>> GetByShiftIdAsync(
        Guid shiftId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var (clampedPage, clampedPageSize) =
            Paging.Clamp(page, pageSize);

        var isManagerOrAdmin =
            _currentUser.Role is Roles.Admin or Roles.Manager;

        var shift = await _dbContext.Shifts
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x =>
                    x.Id == shiftId &&
                    x.TenantId == _currentUser.TenantId &&
                    (isManagerOrAdmin
                        ? x.BranchId == _currentUser.BranchId
                        : x.UserId == _currentUser.UserId),
                cancellationToken);

        if (shift is null)
        {
            throw new KeyNotFoundException(
                "الوردية غير موجودة.");
        }

        var query = _dbContext.Orders
            .AsNoTracking()
            .Where(x => x.ShiftId == shiftId);

        var totalCount = await query.CountAsync(cancellationToken);

        var orders = await query
            .Include(x => x.Lines)
            .Include(x => x.Payments)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Skip((clampedPage - 1) * clampedPageSize)
            .Take(clampedPageSize)
            .ToListAsync(cancellationToken);

        var items = orders
            .Select(order => MapResponse(
                order,
                order.Payments.Sum(x => x.Amount)))
            .ToList();

        return new PagedResult<OrderResponse>(
            items,
            clampedPage,
            clampedPageSize,
            totalCount);
    }

    public async Task<PagedResult<OrderResponse>> GetCurrentShiftOrdersAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var shift = await GetOpenShiftAsync(cancellationToken);

        if (shift is null)
        {
            var (clampedPage, clampedPageSize) = Paging.Clamp(page, pageSize);
            return new PagedResult<OrderResponse>([], clampedPage, clampedPageSize, 0);
        }

        return await GetByShiftIdAsync(shift.Id, page, pageSize, cancellationToken);
    }

    public async Task<OrderResponse> VoidAsync(
        Guid id,
        VoidOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        var order = await _dbContext.Orders
            .Include(x => x.Lines)
            .Include(x => x.Payments)
            .SingleOrDefaultAsync(
                x =>
                    x.Id == id &&
                    x.TenantId == _currentUser.TenantId,
                cancellationToken);

        if (order is null)
        {
            throw new KeyNotFoundException(
                "الفاتورة غير موجودة.");
        }

        if (order.Status != OrderStatus.Completed)
        {
            throw new InvalidOperationException(
                "لا يمكن إلغاء هذه الفاتورة - حالتها الحالية لا تسمح بذلك.");
        }

        var isManagerOrAdmin =
            _currentUser.Role is Roles.Admin or Roles.Manager;

        if (!isManagerOrAdmin)
        {
            var shift = await _dbContext.Shifts
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x => x.Id == order.ShiftId,
                    cancellationToken);

            var canVoid =
                shift is not null &&
                shift.UserId == _currentUser.UserId &&
                shift.Status == ShiftStatus.Open;

            if (!canVoid)
            {
                throw new InvalidOperationException(
                    "لا يمكنك إلغاء هذه الفاتورة - الوردية مقفولة أو ليست وردية بتاعتك، تواصل مع المدير.");
            }
        }

        // Restore stock for any stock-tracked products on the order.
        var productIds = order.Lines
            .Select(x => x.ProductId)
            .Distinct()
            .ToList();

        var products = await _dbContext.Products
            .Where(x => productIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        foreach (var line in order.Lines)
        {
            if (products.TryGetValue(line.ProductId, out var product) &&
                product.TrackStock)
            {
                product.StockQuantity += line.Quantity;
                product.UpdatedAtUtc = DateTime.UtcNow;
            }
        }

        order.Status = OrderStatus.Cancelled;
        order.VoidReason = request.Reason;
        order.VoidedAtUtc = DateTime.UtcNow;
        order.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapResponse(order, order.Payments.Sum(x => x.Amount));
    }

    private Task<Shift?> GetOpenShiftAsync(
        CancellationToken cancellationToken)
    {
        return _dbContext.Shifts
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x =>
                    x.TenantId == _currentUser.TenantId &&
                    x.BranchId == _currentUser.BranchId &&
                    x.UserId == _currentUser.UserId &&
                    x.Status == ShiftStatus.Open,
                cancellationToken);
    }

    private async Task<string> GenerateOrderNumberAsync(
        CancellationToken cancellationToken)
    {
        var today = DateTime.UtcNow.Date;

        var todaysOrderCount = await _dbContext.Orders
            .AsNoTracking()
            .CountAsync(
                x =>
                    x.TenantId == _currentUser.TenantId &&
                    x.CreatedAtUtc >= today,
                cancellationToken);

        return $"{today:yyyyMMdd}-{todaysOrderCount + 1:D5}";
    }

    private void DetachOrderGraph(
        Order order,
        List<OrderLine> orderLines,
        List<Payment> payments)
    {
        _dbContext.Entry(order).State = EntityState.Detached;

        foreach (var line in orderLines)
        {
            _dbContext.Entry(line).State = EntityState.Detached;
        }

        foreach (var payment in payments)
        {
            _dbContext.Entry(payment).State = EntityState.Detached;
        }
    }

    private static OrderResponse MapResponse(
        Order order,
        decimal paidAmount)
    {
        return new OrderResponse(
            Id: order.Id,
            OrderNumber: order.OrderNumber,
            Status: order.Status.ToString(),
            Subtotal: order.Subtotal,
            DiscountAmount: order.DiscountAmount,
            TaxAmount: order.TaxAmount,
            TotalAmount: order.TotalAmount,
            ChangeDue: paidAmount - order.TotalAmount,
            CustomerId: order.CustomerId,
            CreatedAtUtc: order.CreatedAtUtc,
            VoidReason: order.VoidReason,
            VoidedAtUtc: order.VoidedAtUtc,
            Lines: order.Lines
                .Select(line => new OrderLineResponse(
                    Id: line.Id,
                    ProductId: line.ProductId,
                    ProductName: line.ProductName,
                    Quantity: line.Quantity,
                    UnitPrice: line.UnitPrice,
                    DiscountAmount: line.DiscountAmount,
                    TaxAmount: line.TaxAmount,
                    LineTotal: line.LineTotal))
                .ToList(),
            Payments: order.Payments
                .Select(payment => new PaymentResponse(
                    Id: payment.Id,
                    Method: payment.Method.ToString(),
                    Amount: payment.Amount,
                    ReferenceNumber: payment.ReferenceNumber))
                .ToList());
    }
}
