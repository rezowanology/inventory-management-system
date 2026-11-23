using System.Text.Json;
using Ims.Api.Contracts;
using Ims.Api.Data;
using Ims.Api.Domain;
using Ims.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Ims.Api.Services;

/// <summary>Reads the audit log (Admin only). The log is separate from the inventory history and is never written here.</summary>
public class AuditQueryService(ImsDbContext db, AppTime time)
{
    private const string System = "system";

    private IQueryable<AuditLog> Filtered(AuditListQuery query)
    {
        IQueryable<AuditLog> rows = db.AuditLogs.AsNoTracking();
        if (query.ActorId is { } actor) rows = rows.Where(a => a.ActorId == actor);
        if (!string.IsNullOrWhiteSpace(query.Action)) rows = rows.Where(a => a.Action == query.Action);
        if (!string.IsNullOrWhiteSpace(query.EntityType)) rows = rows.Where(a => a.EntityType == query.EntityType);
        if (query.From is { } from) { var start = time.StartOfDayUtc(from); rows = rows.Where(a => a.CreatedAt >= start); }
        if (query.To is { } to) { var end = time.StartOfDayUtc(to.AddDays(1)); rows = rows.Where(a => a.CreatedAt < end); }
        if (query.SearchText is { } text)
        {
            var pattern = ListQuery.Contains(text);
            rows = rows.Where(a => EF.Functions.ILike(a.EntityRepr, pattern) || EF.Functions.ILike(a.Action, pattern)
                || EF.Functions.ILike(a.EntityId, pattern) || (a.Actor != null && EF.Functions.ILike(a.Actor.UserName!, pattern)));
        }
        return query.Sort == "time"
            ? rows.OrderBy(a => a.CreatedAt).ThenBy(a => a.Id)
            : rows.OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.Id);
    }

    private static string ActorLabel(AuditLog a) =>
        a.Actor is null ? System : $"{a.Actor.EmployeeNo} {a.Actor.FirstName} {a.Actor.LastName}";

    public async Task<PagedResult<AuditRowDto>> ListAsync(AuditListQuery query, CancellationToken ct)
    {
        var page = await PagedResult<AuditLog>.CreateAsync(Filtered(query).Include(a => a.Actor), query, ct);
        return page.Map(a => new AuditRowDto(a.Id, a.CreatedAt, a.ActorId, ActorLabel(a), a.Action, a.EntityType, a.EntityId,
            a.EntityRepr, a.IpAddress?.ToString(), a.OldValues != null || a.NewValues != null));
    }

    public async Task<AuditDetailDto?> GetAsync(long id, CancellationToken ct)
    {
        var a = await db.AuditLogs.AsNoTracking().Include(x => x.Actor).FirstOrDefaultAsync(x => x.Id == id, ct);
        return a is null ? null : new AuditDetailDto(a.Id, a.CreatedAt, a.ActorId, ActorLabel(a), a.Action, a.EntityType, a.EntityId,
            a.EntityRepr, a.IpAddress?.ToString(), Parse(a.OldValues), Parse(a.NewValues));
    }

    public async Task<AuditFilterOptionsDto> FilterOptionsAsync(CancellationToken ct) => new(
        await db.Users.AsNoTracking().OrderBy(u => u.EmployeeNo)
            .Select(u => new OptionDto(u.Id, u.EmployeeNo + " " + u.FirstName + " " + u.LastName)).ToListAsync(ct),
        await db.AuditLogs.AsNoTracking().Select(a => a.Action).Distinct().OrderBy(a => a).ToListAsync(ct),
        await db.AuditLogs.AsNoTracking().Select(a => a.EntityType).Distinct().OrderBy(a => a).ToListAsync(ct));

    /// <summary>Every row that matches the filters (no paging), as an export.</summary>
    public async Task<ExportTable> ExportAsync(AuditListQuery query, CancellationToken ct)
    {
        var rows = await Filtered(query).Include(a => a.Actor).ToListAsync(ct);
        return new ExportTable("audit-log",
            ["Time", "Actor", "Action", "Entity type", "Entity id", "Entity", "IP address", "Old values", "New values"],
            rows.Select(a => new object?[] { time.Format(a.CreatedAt), ActorLabel(a), a.Action, a.EntityType, a.EntityId,
                a.EntityRepr, a.IpAddress?.ToString(), a.OldValues, a.NewValues }).ToList());
    }

    private static JsonElement? Parse(string? json) => json is null ? null : JsonSerializer.Deserialize<JsonElement>(json);
}
