using System.Net;

namespace Ims.Api.Domain;

/// <summary>Append-only record of who changed what (and logins). Separate from the inventory history.</summary>
public class AuditLog
{
    public long Id { get; set; }
    /// <summary>Null means the system did it (e.g. seeding or a failed login for an unknown name).</summary>
    public long? ActorId { get; set; }
    public ApplicationUser? Actor { get; set; }
    public string Action { get; set; } = "";
    public string EntityType { get; set; } = "";
    public string EntityId { get; set; } = "";
    public string EntityRepr { get; set; } = "";
    /// <summary>JSON text stored in a jsonb column.</summary>
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public IPAddress? IpAddress { get; set; }
    public DateTime CreatedAt { get; set; }
}
