using Ims.Api.Authorization;
using Ims.Api.Contracts;
using Ims.Api.Infrastructure;
using Ims.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ims.Api.Controllers;

/// <summary>The audit log (Admin only, A7): who changed what, and logins. Separate from the inventory history.</summary>
[Route("api/audit")]
[Authorize(Policy = Capabilities.AuditView)]
public class AuditController(AuditQueryService audit, ExportService export) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<AuditRowDto>>> List([FromQuery] AuditListQuery query, CancellationToken ct)
    {
        if (query.Format is not null) return export.Create(await audit.ExportAsync(query, ct), query.Format);
        return await audit.ListAsync(query, ct);
    }

    [HttpGet("filter-options")]
    public Task<AuditFilterOptionsDto> FilterOptions(CancellationToken ct) => audit.FilterOptionsAsync(ct);

    /// <summary>One row with its old and new values.</summary>
    [HttpGet("{id:long}")]
    public async Task<ActionResult<AuditDetailDto>> Get(long id, CancellationToken ct) =>
        await audit.GetAsync(id, ct) is { } dto ? dto : NotFound();
}
