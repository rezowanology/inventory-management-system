using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Text.Json;
using Ims.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Ims.Api.Services;

/// <summary>
/// Rule R13: every change to master data (products, components, points) and users is written to <c>audit_log</c>
/// with the old and new values of ONLY the properties that changed. It is a <see cref="SaveChangesInterceptor"/>:
/// EF Core calls it around every SaveChanges, and it can look at the ChangeTracker (the list of entities that are
/// about to be INSERTed / UPDATEd / DELETEd).
///
/// - UPDATE and DELETE rows are added to the SAME SaveChanges, so the change and its audit row succeed or fail together.
/// - CREATE rows need the new primary key, which only exists after the INSERT, so they are written in a second, small
///   SaveChanges right afterwards (see DECISIONS.md).
/// - Only a WHITELIST of properties is ever read, so PasswordHash, SecurityStamp, ConcurrencyStamp and the Normalized*
///   columns can never reach the log. A password change is logged as PASSWORD_CHANGE / PASSWORD_RESET with no values.
/// </summary>
public class AuditInterceptor(IHttpContextAccessor http) : SaveChangesInterceptor
{
    private static readonly string[] MasterProps = ["Name", "IsDeleted", "PointType"];
    private static readonly string[] UserProps = ["UserName", "FirstName", "LastName", "EmployeeNo", "Role", "IsActive"];

    /// <summary>Items key a controller can set to "PASSWORD_RESET" so an admin reset is told apart from a user's own change.</summary>
    public const string PasswordActionKey = "audit.password-action";

    private sealed record PendingCreate(object Entity, string Type, Dictionary<string, object?> Values);

    // One interceptor instance serves every DbContext, so the "creates to log after saving" list is kept per context.
    private readonly ConditionalWeakTable<DbContext, List<PendingCreate>> _pending = new();

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Collect(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Collect(eventData.Context);
        return ValueTask.FromResult(result);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        if (TakePending(eventData.Context) is { } creates)
        {
            AddCreateRows(eventData.Context!, creates);
            eventData.Context!.SaveChanges();
        }
        return result;
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        if (TakePending(eventData.Context) is { } creates)
        {
            AddCreateRows(eventData.Context!, creates);
            await eventData.Context!.SaveChangesAsync(cancellationToken);
        }
        return result;
    }

    private List<PendingCreate>? TakePending(DbContext? context)
    {
        if (context is null || !_pending.TryGetValue(context, out var list)) return null;
        _pending.Remove(context); // remove first: the nested save below must not find (and repeat) the same list
        return list.Count > 0 ? list : null;
    }

    // ---- before the save: look at what is about to change ----

    private void Collect(DbContext? context)
    {
        if (context is null) return;
        var entries = context.ChangeTracker.Entries()
            .Where(e => e.Entity is MasterDataEntity or ApplicationUser && e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList(); // a copy: adding AuditLog rows below changes the tracker's list

        foreach (var entry in entries)
        {
            var type = entry.Entity.GetType().Name == nameof(ApplicationUser) ? "User" : entry.Entity.GetType().Name;
            switch (entry.State)
            {
                case EntityState.Added:
                    _pending.GetOrCreateValue(context).Add(new PendingCreate(entry.Entity, type, Snapshot(entry, current: true)));
                    break;
                case EntityState.Deleted:
                    Add(context, "DELETE", type, entry, Snapshot(entry, current: false), null);
                    break;
                case EntityState.Modified:
                    AuditModified(context, entry, type);
                    break;
            }
        }
    }

    private void AuditModified(DbContext context, EntityEntry entry, string type)
    {
        // Compare original and current ourselves: Identity's UpdateAsync marks EVERY column modified.
        var oldValues = new Dictionary<string, object?>();
        var newValues = new Dictionary<string, object?>();
        foreach (var name in WhiteList(entry))
        {
            var p = entry.Property(name);
            if (Equals(p.OriginalValue, p.CurrentValue)) continue;
            oldValues[Camel(name)] = Plain(p.OriginalValue);
            newValues[Camel(name)] = Plain(p.CurrentValue);
        }

        if (oldValues.Count > 0)
            Add(context, ChooseAction(entry, oldValues, newValues), type, entry, oldValues, newValues);

        // A changed password hash is a separate event with NO values (the hash itself is never read).
        if (entry.Entity is ApplicationUser)
        {
            var hash = entry.Property(nameof(ApplicationUser.PasswordHash));
            if (!Equals(hash.OriginalValue, hash.CurrentValue))
                Add(context, http.HttpContext?.Items[PasswordActionKey] as string ?? "PASSWORD_CHANGE", type, entry, null, null);
        }
    }

    /// <summary>Which action name describes this change? Role and (de)activation get their own names; soft delete / restore too.</summary>
    private static string ChooseAction(EntityEntry entry, Dictionary<string, object?> oldValues, Dictionary<string, object?> newValues)
    {
        if (entry.Entity is ApplicationUser)
        {
            if (oldValues.ContainsKey("role")) return "ROLE_CHANGE";
            if (newValues.TryGetValue("isActive", out var active)) return active is true ? "ACTIVATE" : "DEACTIVATE";
        }
        else if (newValues.TryGetValue("isDeleted", out var deleted))
        {
            return deleted is true ? "SOFT_DELETE" : "RESTORE";
        }
        return "UPDATE";
    }

    private void AddCreateRows(DbContext context, List<PendingCreate> creates)
    {
        foreach (var c in creates)
        {
            var entry = context.Entry(c.Entity); // the primary key has been generated by now
            Add(context, "CREATE", c.Type, entry, null, c.Values);
        }
    }

    private void Add(DbContext context, string action, string type, EntityEntry entry,
        Dictionary<string, object?>? oldValues, Dictionary<string, object?>? newValues)
    {
        var httpContext = http.HttpContext;
        long? actor = long.TryParse(httpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null; // null = system
        context.Set<AuditLog>().Add(new AuditLog
        {
            ActorId = actor,
            Action = action,
            EntityType = type,
            EntityId = (entry.Property("Id").CurrentValue ?? "").ToString()!,
            EntityRepr = Truncate(Repr(entry), 300),
            OldValues = oldValues is null ? null : JsonSerializer.Serialize(oldValues),
            NewValues = newValues is null ? null : JsonSerializer.Serialize(newValues),
            IpAddress = httpContext?.Connection.RemoteIpAddress,
        });
    }

    // ---- helpers ----

    private static string[] WhiteList(EntityEntry entry) => entry.Entity switch
    {
        ApplicationUser => UserProps,
        Point => MasterProps,
        _ => MasterProps.Where(n => n != "PointType").ToArray(), // products and components have no point type
    };

    private static Dictionary<string, object?> Snapshot(EntityEntry entry, bool current) =>
        WhiteList(entry).ToDictionary(n => Camel(n), n => Plain(current ? entry.Property(n).CurrentValue : entry.Property(n).OriginalValue));

    /// <summary>A short human label: the name of a product / component / point, or the user name.</summary>
    private static string Repr(EntityEntry entry) => entry.Entity switch
    {
        ApplicationUser u => u.UserName ?? "",
        MasterDataEntity m => m.Name,
        _ => "",
    };

    /// <summary>Enums as text ("ADMIN"), everything else as it is, so the JSON reads well.</summary>
    private static object? Plain(object? value) => value is Enum ? value.ToString() : value;

    private static string Camel(string name) => char.ToLowerInvariant(name[0]) + name[1..];

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];
}
