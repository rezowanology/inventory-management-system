using Ims.Api.Contracts;
using Ims.Api.Data;
using Ims.Api.Domain;
using Ims.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Ims.Api.Services;

/// <summary>
/// CRUD for the three master-data tables (products, components, points). They share one implementation
/// because they share the columns (see <see cref="MasterDataEntity"/>); points add a <see cref="PointType"/>.
/// Rule A9/R11: a row that is referenced by units or history is soft-deleted (archived), otherwise hard-deleted.
/// </summary>
public class MasterDataService(ImsDbContext db)
{
    // ---- reading ----

    /// <summary>The filtered + sorted query shared by the paged list and the export (same filters, so the file matches the screen).</summary>
    private IQueryable<T> BuildListQuery<T>(MasterDataListQuery query) where T : MasterDataEntity
    {
        IQueryable<T> rows = db.Set<T>().AsNoTracking();

        rows = query.Status.ToLowerInvariant() switch
        {
            "deleted" => rows.Where(x => x.IsDeleted),
            "all" => rows,
            _ => rows.Where(x => !x.IsDeleted),
        };
        if (query.Type is not null && typeof(T) == typeof(Point))
            rows = rows.Where(x => ((Point)(object)x).PointType == query.Type);
        if (query.SearchText is { } text)
            rows = rows.Where(x => EF.Functions.ILike(x.Name, ListQuery.Contains(text)));

        // "-name" means descending. The Id tiebreaker keeps paging stable when names are equal.
        var ordered = query.Sort switch
        {
            "-name" => rows.OrderByDescending(x => x.Name).ThenBy(x => x.Id),
            "created" => rows.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id),
            "-created" => rows.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id),
            _ => rows.OrderBy(x => x.Name).ThenBy(x => x.Id),
        };

        return ordered;
    }

    public async Task<PagedResult<MasterDataDto>> ListAsync<T>(MasterDataListQuery query, CancellationToken ct)
        where T : MasterDataEntity =>
        (await PagedResult<T>.CreateAsync(BuildListQuery<T>(query), query, ct)).Map(ToDto);

    /// <summary>Every row that matches the filters (no paging) - the export.</summary>
    public async Task<List<MasterDataDto>> ListAllAsync<T>(MasterDataListQuery query, CancellationToken ct)
        where T : MasterDataEntity =>
        (await BuildListQuery<T>(query).ToListAsync(ct)).Select(ToDto).ToList();

    /// <summary>Dropdown feed: only non-deleted rows, no paging.</summary>
    public async Task<List<OptionDto>> OptionsAsync<T>(PointType? type, CancellationToken ct) where T : MasterDataEntity
    {
        IQueryable<T> rows = db.Set<T>().AsNoTracking().Where(x => !x.IsDeleted);
        if (type is not null && typeof(T) == typeof(Point))
            rows = rows.Where(x => ((Point)(object)x).PointType == type);
        return await rows.OrderBy(x => x.Name).Select(x => new OptionDto(x.Id, x.Name)).ToListAsync(ct);
    }

    public async Task<MasterDataDto?> GetAsync<T>(long id, CancellationToken ct) where T : MasterDataEntity
    {
        var row = await db.Set<T>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return row is null ? null : ToDto(row);
    }

    // ---- writing ----

    public async Task<MasterDataDto> CreateAsync<T>(MasterDataRequest request, CancellationToken ct)
        where T : MasterDataEntity, new()
    {
        var entity = new T { Name = CleanName(request.Name) };
        ApplyType(entity, request.Type);
        await EnsureUniqueAsync(entity, excludeId: null, ct);

        db.Set<T>().Add(entity);
        await db.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    /// <summary>Returns null when the id does not exist.</summary>
    public async Task<MasterDataDto?> UpdateAsync<T>(long id, MasterDataRequest request, CancellationToken ct)
        where T : MasterDataEntity
    {
        var entity = await db.Set<T>().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null) return null;

        var oldType = (entity as Point)?.PointType;
        entity.Name = CleanName(request.Name);
        ApplyType(entity, request.Type);

        // Changing a point's type after it was used would make old history rows nonsense (a TRACK row at a delivery point).
        if (entity is Point p && oldType != p.PointType && await IsInUseAsync(entity, ct))
            throw Invalid("type", "The type of a point that is already used in history cannot be changed.");

        if (!entity.IsDeleted) await EnsureUniqueAsync(entity, excludeId: id, ct);
        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    /// <summary>
    /// In use -> soft delete (Archived = true); not in use -> real DELETE (Archived = false).
    /// Returns null when the id does not exist.
    /// </summary>
    public async Task<DeleteResultDto?> DeleteAsync<T>(long id, CancellationToken ct) where T : MasterDataEntity
    {
        var entity = await db.Set<T>().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null) return null;

        if (await IsInUseAsync(entity, ct))
        {
            if (!entity.IsDeleted)
            {
                entity.IsDeleted = true;
                entity.DeletedAt = DateTime.UtcNow;
                entity.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
            }
            return new DeleteResultDto(Archived: true);
        }

        db.Set<T>().Remove(entity);
        await db.SaveChangesAsync(ct);
        return new DeleteResultDto(Archived: false);
    }

    /// <summary>Brings an archived row back. Returns null when the id does not exist.</summary>
    public async Task<MasterDataDto?> RestoreAsync<T>(long id, CancellationToken ct) where T : MasterDataEntity
    {
        var entity = await db.Set<T>().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null) return null;
        if (!entity.IsDeleted) return ToDto(entity);

        // Someone may have created a new row with the same name while this one was archived.
        entity.IsDeleted = false;
        await EnsureUniqueAsync(entity, excludeId: id, ct);
        entity.DeletedAt = null;
        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    // ---- helpers ----

    /// <summary>Is this row referenced by a unit (products) or a history row (components, points)?</summary>
    private Task<bool> IsInUseAsync(MasterDataEntity entity, CancellationToken ct) => entity switch
    {
        Product => db.Units.AnyAsync(u => u.ProductId == entity.Id, ct),
        Component => db.InventoryTransactions.AnyAsync(t => t.ComponentId == entity.Id, ct),
        Point => db.InventoryTransactions.AnyAsync(t => t.PointId == entity.Id, ct),
        _ => Task.FromResult(false),
    };

    /// <summary>
    /// Case-insensitive uniqueness among non-deleted rows (points: per type). This friendly check runs first;
    /// the partial unique index in the database is the safety net for two requests racing each other.
    /// </summary>
    private async Task EnsureUniqueAsync<T>(T entity, long? excludeId, CancellationToken ct) where T : MasterDataEntity
    {
        var lower = entity.Name.ToLower();
        var skip = excludeId ?? 0;
        IQueryable<T> others = db.Set<T>().Where(x => !x.IsDeleted && x.Id != skip && x.Name.ToLower() == lower);
        if (entity is Point p)
            others = others.Where(x => ((Point)(object)x).PointType == p.PointType);

        if (await others.AnyAsync(ct))
            throw Invalid("name", $"\"{entity.Name}\" already exists. Names must be unique (upper/lower case is ignored).");
    }

    private static string CleanName(string name)
    {
        var clean = name.Trim();
        if (clean.Length < 2) throw Invalid("name", "The name must have at least 2 characters.");
        return clean;
    }

    private static void ApplyType(MasterDataEntity entity, PointType? type)
    {
        if (entity is not Point point) return;
        if (type is null) throw Invalid("type", "Choose a point type (TRACK, RECEIVE or DELIVERY).");
        point.PointType = type.Value;
    }

    /// <summary>A business-rule error (HTTP 422) that the React form shows under the given field.</summary>
    private static DomainException Invalid(string field, string message) =>
        new("validation", message) { Errors = new Dictionary<string, string[]> { [field] = [message] } };

    private static MasterDataDto ToDto(MasterDataEntity x) =>
        new(x.Id, x.Name, x.IsDeleted, x.DeletedAt, x.CreatedAt, x.UpdatedAt, (x as Point)?.PointType.ToString());
}
