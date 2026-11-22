using Ims.Api.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ims.Api.Controllers;

// Every endpoint of the API contract exists from Day 1 with its REAL authorization policy.
// Behaviour is added on the day named in the 501 message. (Stub honesty: a 501 is never a fake success.)

[Route("api/units")]
public class UnitsController : ApiControllerBase
{
    [Authorize(Policy = Capabilities.OpsLookup)]
    [HttpGet("lookup")]
    public IActionResult Lookup([FromQuery] string? barcode) => NotBuilt(3);
}

[Route("api/ops")]
public class OpsController : ApiControllerBase
{
    [Authorize(Policy = Capabilities.OpsTrack)]
    [HttpPost("track")]
    public IActionResult Track() => NotBuilt(3);

    [Authorize(Policy = Capabilities.OpsReceive)]
    [HttpPost("receive")]
    public IActionResult Receive() => NotBuilt(3);

    [Authorize(Policy = Capabilities.OpsDeliver)]
    [HttpPost("deliver")]
    public IActionResult Deliver() => NotBuilt(3);
}

[Route("api/reports")]
[Authorize(Policy = Capabilities.ReportsView)]
public class ReportsController : ApiControllerBase
{
    [HttpGet("stock")] public IActionResult Stock() => NotBuilt(4);
    [HttpGet("summary")] public IActionResult Summary() => NotBuilt(4);
    [HttpGet("barcode")] public IActionResult Barcode() => NotBuilt(4);
}

/// <summary>Shared shape of the three master-data controllers (products, components, points).</summary>
[Authorize(Policy = Capabilities.MasterDataManage)]
public abstract class MasterDataStubController : ApiControllerBase
{
    [HttpGet] public IActionResult List() => NotBuilt(2);
    [HttpGet("{id:long}")] public IActionResult Get(long id) => NotBuilt(2);
    [HttpPost] public IActionResult Create() => NotBuilt(2);
    [HttpPut("{id:long}")] public IActionResult Update(long id) => NotBuilt(2);
    [HttpDelete("{id:long}")] public IActionResult Delete(long id) => NotBuilt(2);
    [HttpPost("{id:long}/restore")] public IActionResult Restore(long id) => NotBuilt(2);
}

[Route("api/products")] public class ProductsController : MasterDataStubController { }
[Route("api/components")] public class ComponentsController : MasterDataStubController { }
[Route("api/points")] public class PointsController : MasterDataStubController { }

[Route("api/users")]
[Authorize(Policy = Capabilities.UsersManage)]
public class UsersController : ApiControllerBase
{
    [HttpGet] public IActionResult List() => NotBuilt(2);
    [HttpGet("{id:long}")] public IActionResult Get(long id) => NotBuilt(2);
    [HttpPost] public IActionResult Create() => NotBuilt(2);
    [HttpPut("{id:long}")] public IActionResult Update(long id) => NotBuilt(2);
    [HttpPost("{id:long}/toggle-active")] public IActionResult ToggleActive(long id) => NotBuilt(2);
    [HttpPost("{id:long}/reset-password")] public IActionResult ResetPassword(long id) => NotBuilt(2);
}

[Route("api/audit")]
[Authorize(Policy = Capabilities.AuditView)]
public class AuditController : ApiControllerBase
{
    [HttpGet] public IActionResult List() => NotBuilt(5);
    [HttpGet("{id:long}")] public IActionResult Get(long id) => NotBuilt(5);
}
