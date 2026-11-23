using System.Security.Claims;
using Ims.Api.Data;
using Ims.Api.Domain;

namespace Ims.Api.Services;

/// <summary>
/// Writes the audit rows that are not data changes: LOGIN, LOGIN_FAILED and LOGOUT (R13). Never receives a password:
/// a failed login records only the user name that was typed.
/// </summary>
public class AuditWriter(ImsDbContext db, IHttpContextAccessor http)
{
    public async Task WriteAsync(string action, long? actorId, string entityType, string entityId, string repr, CancellationToken ct = default)
    {
        db.AuditLogs.Add(new AuditLog
        {
            ActorId = actorId, Action = action, EntityType = entityType, EntityId = entityId,
            EntityRepr = repr.Length <= 300 ? repr : repr[..300],
            IpAddress = http.HttpContext?.Connection.RemoteIpAddress,
        });
        await db.SaveChangesAsync(ct);
    }

    /// <summary>The signed-in user's id from the cookie, or null.</summary>
    public long? CurrentUserId =>
        long.TryParse(http.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
