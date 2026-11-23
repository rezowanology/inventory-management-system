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
public class DashboardController(DashboardService dashboard, IAuthorizationService authorization) : ApiControllerBase
{
    /// <summary>Read-only for every signed-in role. The recent-activity list is only included for roles with history.view.</summary>
    [Authorize(Policy = Capabilities.Dashboard)]
    [HttpGet]
    public async Task<DashboardResponse> Get(CancellationToken ct) =>
        await dashboard.GetMetricsAsync((await authorization.AuthorizeAsync(User, Capabilities.HistoryView)).Succeeded, ct);
}
