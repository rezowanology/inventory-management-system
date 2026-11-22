using System.Security.Claims;
using Ims.Api.Authorization;
using Ims.Api.Contracts;
using Ims.Api.Infrastructure;
using Ims.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ims.Api.Controllers;

[Route("api/users")]
[Authorize(Policy = Capabilities.UsersManage)]
public class UsersController(UserAdminService service) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<UserDto>>> List([FromQuery] UserListQuery query, CancellationToken ct)
    {
        if (query.Format is not null) return NotBuilt(4); // CSV/Excel export arrives on Day 4
        return await service.ListAsync(query, ct);
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<UserDto>> Get(long id) =>
        await service.GetAsync(id) is { } dto ? dto : NotFound();

    [HttpPost]
    public async Task<IActionResult> Create(CreateUserRequest request, CancellationToken ct)
    {
        var dto = await service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = dto.Id }, dto);
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<UserDto>> Update(long id, UpdateUserRequest request, CancellationToken ct) =>
        await service.UpdateAsync(id, request, ct) is { } dto ? dto : NotFound();

    /// <summary>Activate or deactivate (users are never deleted). Deactivation signs the user out on their next request.</summary>
    [HttpPost("{id:long}/toggle-active")]
    public async Task<ActionResult<UserDto>> ToggleActive(long id, CancellationToken ct)
    {
        // The id of the signed-in admin comes from the cookie's NameIdentifier claim.
        long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actingUserId);
        return await service.ToggleActiveAsync(id, actingUserId, ct) is { } dto ? dto : NotFound();
    }

    [HttpPost("{id:long}/reset-password")]
    public async Task<IActionResult> ResetPassword(long id, ResetPasswordRequest request) =>
        await service.ResetPasswordAsync(id, request.NewPassword) ? NoContent() : NotFound();
}
