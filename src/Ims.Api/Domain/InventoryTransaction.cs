namespace Ims.Api.Domain;

/// <summary>One row of the append-only inventory history. Never updated or deleted; reversals ADD a REVERSED row.</summary>
public class InventoryTransaction
{
    public long Id { get; set; }
    public long UnitId { get; set; }
    public Unit? Unit { get; set; }
    public EventType EventType { get; set; }
    public long PointId { get; set; }
    public Point? Point { get; set; }
    public long? ComponentId { get; set; }
    public Component? Component { get; set; }
    public string? ComponentBarcode { get; set; }
    /// <summary>For REVERSED rows: the id of the row being cancelled (unique: a row is reversed at most once).</summary>
    public long? ReversesId { get; set; }
    public InventoryTransaction? Reverses { get; set; }
    public string Reason { get; set; } = "";
    public long PerformedById { get; set; }
    public ApplicationUser? PerformedBy { get; set; }
    public DateTime OccurredAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
