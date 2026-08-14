namespace PosFlow.Application.Orders;

public interface IReceiptPdfService
{
    /// <summary>Renders a printable PDF receipt for a completed/voided order. Throws KeyNotFoundException if the order doesn't exist (or belongs to another tenant).</summary>
    Task<byte[]> GenerateReceiptPdfAsync(
        Guid orderId,
        CancellationToken cancellationToken = default);
}
