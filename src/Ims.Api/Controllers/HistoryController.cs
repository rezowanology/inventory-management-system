using Ims.Api.Authorization;
using Ims.Api.Contracts;
using Ims.Api.Data;
using Ims.Api.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ims.Api.Controllers;

[Route("api/history")]
public class HistoryController(ImsDbContext db) : ApiControllerBase
{
    /// <summary>All history rows, newest first, with simple paging. Filters arrive on Day 3.</summary>
    [Authorize(Policy = Capabilities.HistoryView)]
    [HttpGet]
    public async Task<PagedResult<HistoryRowDto>> List([FromQuery] ListQuery query, CancellationToken ct)
    {
        var rows = db.InventoryTransactions.AsNoTracking();
        var total = await rows.CountAsync(ct);

        var items = await rows
            .OrderByDescending(t => t.OccurredAt).ThenByDescending(t => t.Id)
            .Skip((query.SafePage - 1) * query.SafePageSize).Take(query.SafePageSize)
            .Select(t => new HistoryRowDto(
                t.Id, t.Unit!.Barcode, t.Unit.Product!.Name, t.EventType.ToString(), t.Point!.Name,
                t.Component != null ? t.Component.Name : null, t.ComponentBarcode, t.ReversesId, t.Reason,
                t.PerformedBy!.EmployeeNo, t.PerformedBy.FirstName + " " + t.PerformedBy.LastName, t.OccurredAt))
            .ToListAsync(ct);

        return new PagedResult<HistoryRowDto>(items, query.SafePage, query.SafePageSize, total);
    }

    [Authorize(Policy = Capabilities.HistoryReverse)]
    [HttpGet("{id:long}/reverse-preview")]
    public IActionResult ReversePreview(long id) => NotBuilt(4);

    [Authorize(Policy = Capabilities.HistoryReverse)]
    [HttpPost("{id:long}/reverse")]
    public IActionResult Reverse(long id) => NotBuilt(4);
}
