using Ims.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Ims.Api.Services;

/// <summary>
/// First line of defence for rule R6: refuses to save any UPDATE or DELETE of inventory history or audit rows.
/// (The second line is a PostgreSQL trigger, so even raw SQL cannot change them.)
/// A SaveChangesInterceptor is EF Core's hook that runs just before changes are written.
/// </summary>
public class AppendOnlyInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Check(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Check(eventData.Context);
        return ValueTask.FromResult(result);
    }

    private static void Check(DbContext? context)
    {
        if (context is null) return;
        var bad = context.ChangeTracker.Entries()
            .FirstOrDefault(e => (e.Entity is InventoryTransaction || e.Entity is AuditLog)
                                 && (e.State == EntityState.Modified || e.State == EntityState.Deleted));
        if (bad is not null)
            throw new InvalidOperationException(
                $"{bad.Metadata.ClrType.Name} rows are append-only and cannot be {bad.State.ToString().ToLowerInvariant()}.");
    }
}
