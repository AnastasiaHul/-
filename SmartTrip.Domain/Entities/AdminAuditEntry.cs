namespace SmartTrip.Domain.Entities;

public sealed class AdminAuditEntry
{
    public int AdminAuditEntryId { get; set; }
    public int ActorId { get; set; }
    public string Action { get; set; } = string.Empty;
    public int TargetId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime OccurredAtUtc { get; set; }
}
