using Ims.Api.Authorization;
using Ims.Api.Contracts;
using Ims.Api.Data;
using Ims.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ims.Api.Controllers;

[Route("api/config")]
public class ConfigController(IConfiguration config) : ApiControllerBase
{
    /// <summary>The display time zone (A12). Public so the login page can use it too.</summary>
    [AllowAnonymous]
    [HttpGet]
    public ConfigResponse Get() => new(config["App:TimeZone"] ?? "UTC");
}

[Route("api/dashboard")]
public class DashboardController(ImsDbContext db, InventoryService inventory) : ApiControllerBase
{
    /// <summary>Day 1 version: total units and current stock count. More tiles come on Day 5.</summary>
    [Authorize(Policy = Capabilities.Dashboard)]
    [HttpGet]
    public async Task<DashboardResponse> Get(CancellationToken ct) =>
        new(await db.Units.CountAsync(ct), await inventory.CountInStockAsync(ct));
}
