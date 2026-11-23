using System.Security.Claims;
using Ims.Api.Authorization;
using Ims.Api.Contracts;
using Ims.Api.Data;
using Ims.Api.Domain;
using Ims.Api.Infrastructure;
using Ims.Api.Services;
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
public class HistoryController(
    ImsDbContext db, InventoryService inventory, AppTime time, ExportService export, IAuthorizationService authorization)
    : ApiControllerBase
{
    private long CurrentUserId => long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>History rows, newest first. Filters: event, point, product, user, date range; search: barcode or component barcode. ?format=csv|xlsx exports the same rows without paging.</summary>
    [Authorize(Policy = Capabilities.HistoryView)]
    [HttpGet]
    public async Task<ActionResult<PagedResult<HistoryRowDto>>> List([FromQuery] HistoryListQuery query, CancellationToken ct)
    {
        IQueryable<InventoryTransaction> rows = db.InventoryTransactions.AsNoTracking();
        if (query.EventType is { } ev) rows = rows.Where(t => t.EventType == ev);
        if (query.PointId is { } point) rows = rows.Where(t => t.PointId == point);
        if (query.ProductId is { } product) rows = rows.Where(t => t.Unit!.ProductId == product);
        if (query.UserId is { } user) rows = rows.Where(t => t.PerformedById == user);

        // Day boundaries are local days in App:TimeZone, converted to UTC for the timestamptz column.
        if (query.From is { } from) { var start = time.StartOfDayUtc(from); rows = rows.Where(t => t.OccurredAt >= start); }
        if (query.To is { } to) { var end = time.StartOfDayUtc(to.AddDays(1)); rows = rows.Where(t => t.OccurredAt < end); }

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

        // Export: the same filtered rows, all of them, no paging.
        if (query.Format is not null)
            return export.Create(HistoryQueries.ToExport("history", await ordered.ToDtos(db).ToListAsync(ct), time), query.Format);

        var page = await PagedResult<HistoryRowDto>.CreateAsync(ordered.ToDtos(db), query, ct);

        // canReverse is only worked out for admins (it costs a few extra queries and nobody else may reverse anyway).
        if ((await authorization.AuthorizeAsync(User, Capabilities.HistoryReverse)).Succeeded)
        {
            var reversible = await inventory.ReversibleRowIdsAsync(page.Items.Select(r => r.UnitBarcode), ct);
            return page.Map(r => r with { CanReverse = reversible.Contains(r.Id) });
        }
        return page;
    }

    /// <summary>Dropdown contents for the history filters. Needed here because the Report Viewer may not call the /options endpoints.</summary>
    [Authorize(Policy = Capabilities.HistoryView)]
    [HttpGet("filter-options")]
    public async Task<HistoryFilterOptionsDto> FilterOptions(CancellationToken ct) => new(
        await db.Products.AsNoTracking().OrderBy(p => p.Name).Select(p => new OptionDto(p.Id, p.Name)).ToListAsync(ct),
        await db.Points.AsNoTracking().OrderBy(p => p.Name).Select(p => new OptionDto(p.Id, p.Name)).ToListAsync(ct),
        await db.Users.AsNoTracking().OrderBy(u => u.EmployeeNo)
            .Select(u => new OptionDto(u.Id, u.EmployeeNo + " " + u.FirstName + " " + u.LastName)).ToListAsync(ct));

    /// <summary>The confirmation screen: the row, whether it may be reversed (and if not, why), and what would change.</summary>
    [Authorize(Policy = Capabilities.HistoryReverse)]
    [HttpGet("{id:long}/reverse-preview")]
    public async Task<ReversePreviewDto> ReversePreview(long id, CancellationToken ct)
    {
        var (_, check) = await inventory.PreviewReversalAsync(id, ct); // throws NotFoundException -> 404
        var row = await db.InventoryTransactions.AsNoTracking().Where(t => t.Id == id).ToDtos(db).FirstAsync(ct);
        return new ReversePreviewDto(row with { CanReverse = check.Allowed }, check.Allowed, check.BlockedReason, check.Effect);
    }

    /// <summary>Adds a REVERSED row (never changes the old one). A blocked reversal is a 422 with the reason.</summary>
    [Authorize(Policy = Capabilities.HistoryReverse)]
    [HttpPost("{id:long}/reverse")]
    public async Task<ReverseResultDto> Reverse(long id, ReverseRequest request, CancellationToken ct)
    {
        var result = await inventory.ReverseAsync(id, request.Reason, CurrentUserId, ct);
        return new ReverseResultDto(result.ReversalId, result.UnitBarcode, result.ReversedEventType, result.Effect);
    }
}
