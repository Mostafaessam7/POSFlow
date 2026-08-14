using Microsoft.EntityFrameworkCore;
using PosFlow.Application.Common;
using PosFlow.Application.Orders;
using PosFlow.Domain.Entities;
using PosFlow.Infrastructure.Persistence;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PosFlow.Infrastructure.Orders;

/// <summary>
/// Renders a printable PDF receipt with QuestPDF. QuestPDF's Community
/// license is free for organizations with under $1M/year revenue (or
/// non-profit/personal use) - see https://www.questpdf.com/license/.
/// If PosFlow is ever used by a larger company, either buy a QuestPDF
/// Professional/Enterprise license or swap this implementation for a
/// different PDF library; nothing else in the codebase depends on
/// QuestPDF specifically.
/// </summary>
public sealed class ReceiptPdfService(
    PosFlowDbContext dbContext,
    ICurrentUser currentUser)
    : IReceiptPdfService
{
    private readonly PosFlowDbContext _dbContext = dbContext;
    private readonly ICurrentUser _currentUser = currentUser;

    static ReceiptPdfService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public async Task<byte[]> GenerateReceiptPdfAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        var order = await _dbContext.Orders
            .AsNoTracking()
            .Include(x => x.Lines)
            .Include(x => x.Payments)
            .SingleOrDefaultAsync(
                x =>
                    x.Id == orderId &&
                    x.TenantId == _currentUser.TenantId,
                cancellationToken);

        if (order is null)
        {
            throw new KeyNotFoundException("الفاتورة غير موجودة.");
        }

        var branch = await _dbContext.Branches
            .AsNoTracking()
            .SingleAsync(x => x.Id == order.BranchId, cancellationToken);

        var tenant = await _dbContext.Tenants
            .AsNoTracking()
            .SingleAsync(x => x.Id == order.TenantId, cancellationToken);

        var paidAmount = order.Payments.Sum(x => x.Amount);

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A6);
                page.Margin(15);
                page.DefaultTextStyle(x => x.FontSize(9));
                page.ContentFromRightToLeft();

                page.Header().Column(column =>
                {
                    column.Item().AlignCenter().Text(tenant.Name).FontSize(14).Bold();
                    column.Item().AlignCenter().Text(branch.Name).FontSize(10);
                    column.Item().PaddingTop(5).LineHorizontal(1);
                });

                page.Content().Column(column =>
                {
                    column.Spacing(3);

                    column.Item().PaddingTop(5).Row(row =>
                    {
                        row.RelativeItem().Text($"رقم الفاتورة: {order.OrderNumber}");
                        row.RelativeItem().AlignLeft().Text(order.CreatedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm"));
                    });

                    if (order.Status == OrderStatus.Cancelled)
                    {
                        column.Item().Text("فاتورة ملغاة").FontColor(Colors.Red.Medium).Bold();
                    }

                    column.Item().PaddingTop(5).LineHorizontal(0.5f);

                    foreach (var line in order.Lines)
                    {
                        column.Item().Row(row =>
                        {
                            row.RelativeItem(3).Text($"{line.ProductName} x{line.Quantity:0.##}");
                            row.RelativeItem(1).AlignLeft().Text($"{line.LineTotal:0.00}");
                        });
                    }

                    column.Item().PaddingTop(5).LineHorizontal(0.5f);

                    column.Item().Row(row =>
                    {
                        row.RelativeItem().Text("الإجمالي الفرعي");
                        row.RelativeItem().AlignLeft().Text($"{order.Subtotal:0.00}");
                    });

                    if (order.DiscountAmount > 0)
                    {
                        column.Item().Row(row =>
                        {
                            row.RelativeItem().Text("الخصم");
                            row.RelativeItem().AlignLeft().Text($"-{order.DiscountAmount:0.00}");
                        });
                    }

                    if (order.TaxAmount > 0)
                    {
                        column.Item().Row(row =>
                        {
                            row.RelativeItem().Text("الضريبة");
                            row.RelativeItem().AlignLeft().Text($"{order.TaxAmount:0.00}");
                        });
                    }

                    column.Item().PaddingTop(3).Row(row =>
                    {
                        row.RelativeItem().Text("الإجمالي").Bold().FontSize(11);
                        row.RelativeItem().AlignLeft().Text($"{order.TotalAmount:0.00} {tenant.CurrencyCode}").Bold().FontSize(11);
                    });

                    column.Item().PaddingTop(5).LineHorizontal(0.5f);

                    foreach (var payment in order.Payments)
                    {
                        column.Item().Row(row =>
                        {
                            row.RelativeItem().Text(TranslatePaymentMethod(payment.Method));
                            row.RelativeItem().AlignLeft().Text($"{payment.Amount:0.00}");
                        });
                    }

                    var changeDue = paidAmount - order.TotalAmount;

                    if (changeDue > 0)
                    {
                        column.Item().Row(row =>
                        {
                            row.RelativeItem().Text("الباقي");
                            row.RelativeItem().AlignLeft().Text($"{changeDue:0.00}");
                        });
                    }
                });

                page.Footer().Column(column =>
                {
                    column.Item().PaddingTop(8).LineHorizontal(0.5f);
                    column.Item().AlignCenter().PaddingTop(5).Text("شكرًا لتعاملكم معنا").FontSize(9);
                });
            });
        });

        return document.GeneratePdf();
    }

    private static string TranslatePaymentMethod(PaymentMethod method)
    {
        return method switch
        {
            PaymentMethod.Cash => "نقدًا",
            PaymentMethod.Card => "بطاقة",
            _ => method.ToString()
        };
    }
}
