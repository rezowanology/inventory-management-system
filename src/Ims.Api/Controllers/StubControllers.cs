using Ims.Api.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ims.Api.Controllers;

// Endpoints of the API contract exist from Day 1 with their REAL authorization policy.
// Behaviour is added on the day named in the 501 message. (Stub honesty: a 501 is never a fake success.)
// Day 4 moved the reports to ReportsController.cs; only the audit log is still a stub.

[Route("api/audit")]
[Authorize(Policy = Capabilities.AuditView)]
public class AuditController : ApiControllerBase
{
    [HttpGet] public IActionResult List() => NotBuilt(5);
    [HttpGet("{id:long}")] public IActionResult Get(long id) => NotBuilt(5);
}
