using Ims.Api.Authorization;
using Ims.Api.Contracts;
using Ims.Api.Domain;
using Ims.Api.Infrastructure;
using Ims.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ims.Api.Controllers;

/// <summary>
/// The shared endpoints of products, components and points. The generic parameter says which table is meant;
/// the three tiny classes at the bottom only add the URL. Controllers stay thin: the rules live in MasterDataService.
/// Authorization is on each action (not on the class) because /options is also needed by non-admin screens.
/// </summary>
public abstract class MasterDataController<T>(MasterDataService service) : ApiControllerBase
    where T : MasterDataEntity, new()
{
    [Authorize(Policy = Capabilities.MasterDataManage)]
    [HttpGet]
    public async Task<ActionResult<PagedResult<MasterDataDto>>> List([FromQuery] MasterDataListQuery query, CancellationToken ct)
    {
        if (query.Format is not null) return NotBuilt(4); // CSV/Excel export arrives on Day 4
        return await service.ListAsync<T>(query, ct);
    }

    /// <summary>Feeds every dropdown (ops screens too): non-deleted rows only.</summary>
    [Authorize(Policy = Capabilities.OpsLookup)]
    [HttpGet("options")]
    public async Task<List<OptionDto>> Options([FromQuery] PointType? type, CancellationToken ct) =>
        await service.OptionsAsync<T>(type, ct);

    [Authorize(Policy = Capabilities.MasterDataManage)]
    [HttpGet("{id:long}")]
    public async Task<ActionResult<MasterDataDto>> Get(long id, CancellationToken ct) =>
        await service.GetAsync<T>(id, ct) is { } dto ? dto : NotFound();

    [Authorize(Policy = Capabilities.MasterDataManage)]
    [HttpPost]
    public async Task<IActionResult> Create(MasterDataRequest request, CancellationToken ct)
    {
        var dto = await service.CreateAsync<T>(request, ct);
        return CreatedAtAction(nameof(Get), new { id = dto.Id }, dto);
    }

    [Authorize(Policy = Capabilities.MasterDataManage)]
    [HttpPut("{id:long}")]
    public async Task<ActionResult<MasterDataDto>> Update(long id, MasterDataRequest request, CancellationToken ct) =>
        await service.UpdateAsync<T>(id, request, ct) is { } dto ? dto : NotFound();

    /// <summary>In use -> archived (soft delete, {archived: true}); not in use -> really deleted ({archived: false}).</summary>
    [Authorize(Policy = Capabilities.MasterDataManage)]
    [HttpDelete("{id:long}")]
    public async Task<ActionResult<DeleteResultDto>> Delete(long id, CancellationToken ct) =>
        await service.DeleteAsync<T>(id, ct) is { } result ? result : NotFound();

    [Authorize(Policy = Capabilities.MasterDataManage)]
    [HttpPost("{id:long}/restore")]
    public async Task<ActionResult<MasterDataDto>> Restore(long id, CancellationToken ct) =>
        await service.RestoreAsync<T>(id, ct) is { } dto ? dto : NotFound();
}

[Route("api/products")] public class ProductsController(MasterDataService s) : MasterDataController<Product>(s) { }
[Route("api/components")] public class ComponentsController(MasterDataService s) : MasterDataController<Component>(s) { }
[Route("api/points")] public class PointsController(MasterDataService s) : MasterDataController<Point>(s) { }
