using Ims.Api.Authorization;
using Ims.Api.Contracts;
using Ims.Api.Domain;
using Ims.Api.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Ims.Api.Controllers;

[Route("api/auth")]
public class AuthController(
    UserManager<ApplicationUser> users,
    SignInManager<ApplicationUser> signIn,
    IAntiforgery antiforgery,
    AuditWriter audit) : ApiControllerBase
{
    /// <summary>
    /// Hands the browser an antiforgery token: the cookie ".AspNetCore.Antiforgery" (HttpOnly) is set by the
    /// framework, and we also set a JavaScript-readable "XSRF-TOKEN" cookie. The React client copies its value into
    /// the "X-XSRF-TOKEN" header on every POST/PUT/DELETE, and the server compares the two.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("csrf")]
    public IActionResult Csrf()
    {
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        Response.Cookies.Append("XSRF-TOKEN", tokens.RequestToken!, new CookieOptions
        {
            HttpOnly = false, // the SPA must be able to read it
            SameSite = SameSiteMode.Strict,
            Secure = Request.IsHttps
        });
        return NoContent();
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var user = await users.FindByNameAsync(request.UserName);
        // Same answer for "no such user" and "wrong password" so attackers cannot probe user names.
        if (user is null || !await users.CheckPasswordAsync(user, request.Password))
        {
            // Only the typed user name is recorded, never the password.
            await audit.WriteAsync("LOGIN_FAILED", user?.Id, "User", user?.Id.ToString() ?? "", request.UserName ?? "");
            return Problem(statusCode: 401, title: "Invalid user name or password");
        }

        if (!user.IsActive)
            return Problem(statusCode: 403, title: "This account is not active",
                detail: "This account is not active. Ask an Admin to activate it.");

        await signIn.SignInAsync(user, isPersistent: false); // writes the auth cookie
        user.LastLoginAt = DateTime.UtcNow; // lets the admin list tell "pending registration" from "deactivated"
        await users.UpdateAsync(user);
        await audit.WriteAsync("LOGIN", user.Id, "User", user.Id.ToString(), user.UserName ?? "");
        return Ok(ToMe(user));
    }

    [AllowAnonymous]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        var who = audit.CurrentUserId is { } id ? await users.FindByIdAsync(id.ToString()) : null;
        await signIn.SignOutAsync();
        if (who is not null) await audit.WriteAsync("LOGOUT", who.Id, "User", who.Id.ToString(), who.UserName ?? "");
        return NoContent();
    }

    /// <summary>Self-registration (A6): creates an INACTIVE Report Viewer. An Admin must activate it.</summary>
    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        var employeeNo = request.EmployeeNo.Trim();
        if (await users.Users.AnyAsync(u => u.EmployeeNo == employeeNo))
            return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["employeeNo"] = ["This employee number is already registered."]
            }));

        var user = new ApplicationUser
        {
            UserName = request.UserName.Trim(),
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            EmployeeNo = employeeNo,
            Role = Role.REPORT_VIEWER,
            IsActive = false
        };
        var result = await users.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            var errors = result.Errors
                .GroupBy(e => e.Code.Contains("Password") ? "password" : e.Code.Contains("UserName") ? "userName" : "userName")
                .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray());
            return ValidationProblem(new ValidationProblemDetails(errors));
        }
        return StatusCode(201, new { message = "Account created. An Admin must activate it before you can sign in." });
    }

    /// <summary>Who am I + what may I do? The React menu and route guards use ONLY this capability list.</summary>
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var user = await users.GetUserAsync(User);
        return user is null ? Unauthorized() : Ok(ToMe(user));
    }

    /// <summary>
    /// Every role may change their OWN password. ChangePasswordAsync checks the current password, validates and
    /// hashes the new one and renews the security stamp (which invalidates the old cookie), so we sign the user
    /// in again with RefreshSignInAsync to hand out a fresh cookie.
    /// </summary>
    [Authorize(Policy = Capabilities.ChangePassword)]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request)
    {
        var user = await users.GetUserAsync(User);
        if (user is null) return Unauthorized();

        if (request.NewPassword != request.ConfirmPassword)
            return Field("confirmPassword", "The two new passwords do not match.");
        if (request.NewPassword == request.CurrentPassword)
            return Field("newPassword", "The new password must be different from the current one.");

        var result = await users.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
        {
            var errors = result.Errors
                .GroupBy(e => e.Code == "PasswordMismatch" ? "currentPassword" : "newPassword")
                .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray());
            return ValidationProblem(new ValidationProblemDetails(errors));
        }

        await signIn.RefreshSignInAsync(user);
        return Ok(new { message = "Your password was changed." });
    }

    private IActionResult Field(string field, string message) =>
        ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]> { [field] = [message] }));

    private static MeResponse ToMe(ApplicationUser u) =>
        new(u.Id, u.UserName ?? "", u.DisplayName, u.Role.ToString(), u.EmployeeNo, Capabilities.ForRole(u.Role).ToList());
}
