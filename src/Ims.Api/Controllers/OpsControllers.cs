using System.Security.Claims;
using Ims.Api.Authorization;
using Ims.Api.Contracts;
using Ims.Api.Domain;
using Ims.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ims.Api.Controllers;

/// <summary>Look up a barcode (used by every scan screen). Thin: all logic is in InventoryService.</summary>
[Route("api/units")]
public class UnitsController(InventoryService inventory) : ApiControllerBase
{
    [Authorize(Policy = Capabilities.OpsLookup)]
    [HttpGet("lookup")]
    public async Task<ActionResult<UnitLookupDto>> Lookup([FromQuery] string? barcode, CancellationToken ct)
    {
        var code = Barcode.Normalize(barcode);
        if (!Barcode.IsValid(code))
        {
            ModelState.AddModelError("barcode", "Barcode must be 6-32 characters: A-Z, 0-9 and \"-\".");
            return ValidationProblem(ModelState);
        }
        return await inventory.LookupAsync(code, ct);
    }
}

/// <summary>
/// The three scan operations. Each action only checks the request shape ([ApiController] + DataAnnotations),
/// then calls the service. Rule violations come back as DomainException (422) / DuplicateScanException (409),
/// which the shared ApiExceptionHandler turns into ProblemDetails.
/// </summary>
[Route("api/ops")]
public class OpsController(InventoryService inventory) : ApiControllerBase
{
    // The signed-in user's id is stored in the cookie as the NameIdentifier claim.
    private long CurrentUserId => long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [Authorize(Policy = Capabilities.OpsTrack)]
    [HttpPost("track")]
    public Task<UnitLookupDto> Track(TrackRequest request, CancellationToken ct) =>
        inventory.TrackAsync(request.Barcode, request.ProductId, request.TrackPointId,
            request.Components.Select(c => new InventoryService.TrackItem(c.ComponentId!.Value, c.ComponentBarcode)).ToList(),
            CurrentUserId, ct);

    [Authorize(Policy = Capabilities.OpsReceive)]
    [HttpPost("receive")]
    public Task<UnitLookupDto> Receive(ReceiveRequest request, CancellationToken ct) =>
        inventory.ReceiveAsync(request.Barcode, request.ProductId, request.ReceivePointId, CurrentUserId, ct);

    [Authorize(Policy = Capabilities.OpsDeliver)]
    [HttpPost("deliver")]
    public Task<UnitLookupDto> Deliver(DeliverRequest request, CancellationToken ct) =>
        inventory.DeliverAsync(request.Barcode, request.DeliveryPointId, CurrentUserId, ct);
}
