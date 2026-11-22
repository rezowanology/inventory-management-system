using Ims.Api.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ims.Api.Controllers;

// Every endpoint of the API contract exists from Day 1 with its REAL authorization policy.
// Behaviour is added on the day named in the 501 message. (Stub honesty: a 501 is never a fake success.)

[Route("api/reports")]
[Authorize(Policy = Capabilities.ReportsView)]
public class ReportsController : ApiControllerBase
{
    [HttpGet("stock")] public IActionResult Stock() => NotBuilt(4);
    [HttpGet("summary")] public IActionResult Summary() => NotBuilt(4);
    [HttpGet("barcode")] public IActionResult Barcode() => NotBuilt(4);
}

[Route("api/audit")]
[Authorize(Policy = Capabilities.AuditView)]
public class AuditController : ApiControllerBase
{
    [HttpGet] public IActionResult List() => NotBuilt(5);
    [HttpGet("{id:long}")] public IActionResult Get(long id) => NotBuilt(5);
}
