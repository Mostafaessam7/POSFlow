namespace PosFlow.Domain.Entities;

public enum AuditAction
{
    Created = 1,
    Updated = 2,
    Deleted = 3
}

/// <summary>
/// Append-only record of who changed what, and when, for entities that
/// matter for a financial/compliance trail (orders, products, users,
/// branches, shifts - see PosFlowDbContext.AuditedEntityTypes). Never
/// updated or deleted by application code.
/// </summary>
public sealed class AuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? TenantId { get; set; }

    public Guid? UserId { get; set; }

    public string? UserDisplayName { get; set; }

    public string EntityName { get; set; } = null!;

    public string EntityId { get; set; } = null!;

    public AuditAction Action { get; set; }

    /// <summary>JSON: for Updated, {"Field":{"old":..,"new":..}, ...}; for Created/Deleted, the full entity snapshot.</summary>
    public string ChangesJson { get; set; } = "{}";

    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
}
