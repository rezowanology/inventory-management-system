namespace Ims.Api.Domain;

/// <summary>
/// One physical unit identified by its barcode. Identity only: there is deliberately NO status column;
/// status is derived from the history (see InventoryService).
/// </summary>
public class Unit
{
    public long Id { get; set; }
    public string Barcode { get; set; } = "";
    public long ProductId { get; set; }
    public Product? Product { get; set; }
    public DateTime RegisteredAt { get; set; }
    public long RegisteredById { get; set; }
    public ApplicationUser? RegisteredBy { get; set; }
}
