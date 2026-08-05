using Microsoft.EntityFrameworkCore;
using PosFlow.Application.Common;
using PosFlow.Application.Customers;
using PosFlow.Domain.Entities;
using PosFlow.Infrastructure.Persistence;

namespace PosFlow.Infrastructure.Customers;

public sealed class CustomerService(
    PosFlowDbContext dbContext,
    ICurrentUser currentUser)
    : ICustomerService
{
    private readonly PosFlowDbContext _dbContext = dbContext;
    private readonly ICurrentUser _currentUser = currentUser;

    public async Task<PagedResult<CustomerResponse>> GetAllAsync(
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var (clampedPage, clampedPageSize) = Paging.Clamp(page, pageSize);

        var query = _dbContext.Customers
            .AsNoTracking()
            .Where(x => x.TenantId == _currentUser.TenantId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(x =>
                x.Name.Contains(search) ||
                (x.Phone != null && x.Phone.Contains(search)));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var customers = await query
            .OrderBy(x => x.Name)
            .Skip((clampedPage - 1) * clampedPageSize)
            .Take(clampedPageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<CustomerResponse>(
            customers.Select(MapResponse).ToList(),
            clampedPage,
            clampedPageSize,
            totalCount);
    }

    public async Task<CustomerResponse> CreateAsync(
        CreateCustomerRequest request,
        CancellationToken cancellationToken = default)
    {
        var customer = new Customer
        {
            TenantId = _currentUser.TenantId,
            Name = request.Name,
            Phone = request.Phone,
            Email = request.Email
        };

        _dbContext.Customers.Add(customer);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapResponse(customer);
    }

    public async Task<CustomerResponse> UpdateAsync(
        Guid id,
        UpdateCustomerRequest request,
        CancellationToken cancellationToken = default)
    {
        var customer = await _dbContext.Customers
            .SingleOrDefaultAsync(
                x => x.Id == id && x.TenantId == _currentUser.TenantId,
                cancellationToken);

        if (customer is null)
        {
            throw new KeyNotFoundException("العميل غير موجود.");
        }

        customer.Name = request.Name;
        customer.Phone = request.Phone;
        customer.Email = request.Email;
        customer.IsActive = request.IsActive;
        customer.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapResponse(customer);
    }

    private static CustomerResponse MapResponse(Customer customer)
    {
        return new CustomerResponse(
            customer.Id,
            customer.Name,
            customer.Phone,
            customer.Email,
            customer.LoyaltyPoints,
            customer.IsActive);
    }
}
