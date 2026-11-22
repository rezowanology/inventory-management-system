using Ims.Api.Authorization;
using Ims.Api.Contracts;
using Ims.Api.Data;
using Ims.Api.Domain;
using Ims.Api.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ims.Api.Controllers;

/// <summary>History filters on top of the shared list parameters (q, page, pageSize, sort).</summary>
public class HistoryListQuery : ListQuery
{
    public EventType? EventType { get; set; }
    public long? PointId { get; set; }
    public long? ProductId { get; set; }
    public long? UserId { get; set; }
    /// <summary>Inclusive start day, "yyyy-MM-dd", read in the configured display time zone (A12).</summary>
    public DateOnly? From { get; set; }
    /// <summary>Inclusive end day, "yyyy-MM-dd".</summary>
    public DateOnly? To { get; set; }
}

[Route("api/history")]
public class HistoryController(ImsDbContext db, IConfiguration config) : ApiControllerBase
{
    /// <summary>History rows, newest first. Filters: event, point, product, user, date range; search: barcode or component barcode.</summary>
    [Authorize(Policy = Capabilities.HistoryView)]
    [HttpGet]
    public async Task<ActionResult<PagedResult<HistoryRowDto>>> List([FromQuery] HistoryListQuery query, CancellationToken ct)
    {
        if (query.Format is not null) return NotBuilt(4); // CSV/Excel export arrives on Day 4

        IQueryable<InventoryTransaction> rows = db.InventoryTransactions.AsNoTracking();
        if (query.EventType is { } ev) rows = rows.Where(t => t.EventType == ev);
        if (query.PointId is { } point) rows = rows.Where(t => t.PointId == point);
        if (query.ProductId is { } product) rows = rows.Where(t => t.Unit!.ProductId == product);
        if (query.UserId is { } user) rows = rows.Where(t => t.PerformedById == user);

        // Day boundaries are local days in App:TimeZone, converted to UTC for the timestamptz column.
        var zone = ResolveZone();
        if (query.From is { } from) { var start = ToUtc(from, zone); rows = rows.Where(t => t.OccurredAt >= start); }
        if (query.To is { } to) { var end = ToUtc(to.AddDays(1), zone); rows = rows.Where(t => t.OccurredAt < end); }

        if (query.SearchText is { } text)
        {
            var pattern = ListQuery.Contains(text);
            rows = rows.Where(t => EF.Functions.ILike(t.Unit!.Barcode, pattern)
                || (t.ComponentBarcode != null && EF.Functions.ILike(t.ComponentBarcode, pattern))
                || EF.Functions.ILike(t.Unit!.Product!.Name, pattern));
        }

        var ordered = query.Sort == "time"
            ? rows.OrderBy(t => t.OccurredAt).ThenBy(t => t.Id)
            : rows.OrderByDescending(t => t.OccurredAt).ThenByDescending(t => t.Id);

        return await PagedResult<HistoryRowDto>.CreateAsync(ordered.Select(t => new HistoryRowDto(
            t.Id, t.Unit!.Barcode, t.Unit.Product!.Name, t.EventType.ToString(), t.Point!.Name,
            t.Component != null ? t.Component.Name : null, t.ComponentBarcode, t.ReversesId, t.Reason,
            t.PerformedBy!.EmployeeNo, t.PerformedBy.FirstName + " " + t.PerformedBy.LastName, t.OccurredAt)), query, ct);
    }

    /// <summary>Dropdown contents for the history filters. Needed here because the Report Viewer may not call the /options endpoints.</summary>
    [Authorize(Policy = Capabilities.HistoryView)]
    [HttpGet("filter-options")]
    public async Task<HistoryFilterOptionsDto> FilterOptions(CancellationToken ct) => new(
        await db.Products.AsNoTracking().OrderBy(p => p.Name).Select(p => new OptionDto(p.Id, p.Name)).ToListAsync(ct),
        await db.Points.AsNoTracking().OrderBy(p => p.Name).Select(p => new OptionDto(p.Id, p.Name)).ToListAsync(ct),
        await db.Users.AsNoTracking().OrderBy(u => u.EmployeeNo)
            .Select(u => new OptionDto(u.Id, u.EmployeeNo + " " + u.FirstName + " " + u.LastName)).ToListAsync(ct));

    private TimeZoneInfo ResolveZone()
    {
        foreach (var id in new[] { config["App:TimeZone"], "Bangladesh Standard Time" })
        {
            if (string.IsNullOrWhiteSpace(id)) continue;
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); } catch (TimeZoneNotFoundException) { }
        }
        return TimeZoneInfo.Utc;
    }

    private static DateTime ToUtc(DateOnly day, TimeZoneInfo zone) =>
        TimeZoneInfo.ConvertTimeToUtc(day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), zone);

    [Authorize(Policy = Capabilities.HistoryReverse)]
    [HttpGet("{id:long}/reverse-preview")]
    public IActionResult ReversePreview(long id) => NotBuilt(4);

    [Authorize(Policy = Capabilities.HistoryReverse)]
    [HttpPost("{id:long}/reverse")]
    public IActionResult Reverse(long id) => NotBuilt(4);
}
