using Microsoft.AspNetCore.Mvc;

namespace Ims.Api.Controllers;

/// <summary>
/// Base class for API controllers. [ApiController] turns on automatic 400 responses for invalid request bodies;
/// [Route("api/[controller]")] is overridden per controller where the URL differs.
/// </summary>
[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>An honest placeholder: the endpoint, its URL and its authorization policy exist, the behaviour comes later.</summary>
    protected ObjectResult NotBuilt(int day) =>
        StatusCode(StatusCodes.Status501NotImplemented, new ProblemDetails
        {
            Status = StatusCodes.Status501NotImplemented,
            Title = "Not implemented yet",
            Detail = $"Built on Day {day}"
        });
}
