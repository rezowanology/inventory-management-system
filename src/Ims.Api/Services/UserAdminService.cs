using Ims.Api.Contracts;
using Ims.Api.Domain;
using Ims.Api.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Ims.Api.Services;

/// <summary>
/// User administration on top of ASP.NET Core Identity's <see cref="UserManager{TUser}"/>.
/// UserManager hashes passwords, checks the password rules and keeps the "security stamp": a random value
/// stored in the user row AND inside the sign-in cookie. Changing the stamp invalidates every cookie of that
/// user, which is how deactivation logs a person out immediately (rule R12).
/// Users are never deleted, only deactivated.
/// </summary>
public class UserAdminService(UserManager<ApplicationUser> users)
{
    public async Task<PagedResult<UserDto>> ListAsync(UserListQuery query, CancellationToken ct)
    {
        IQueryable<ApplicationUser> rows = users.Users.AsNoTracking();

        if (query.SearchText is { } text)
        {
            var pattern = ListQuery.Contains(text);
            rows = rows.Where(u =>
                EF.Functions.ILike(u.UserName!, pattern) ||
                EF.Functions.ILike(u.FirstName + " " + u.LastName, pattern) ||
                EF.Functions.ILike(u.EmployeeNo, pattern));
        }
        if (query.Role is { } role) rows = rows.Where(u => u.Role == role);
        if (query.Active is { } active) rows = rows.Where(u => u.IsActive == active);
        if (query.Pending == true) rows = rows.Where(u => !u.IsActive && u.LastLoginAt == null);

        var ordered = query.Sort switch
        {
            "-username" => rows.OrderByDescending(u => u.UserName),
            "name" => rows.OrderBy(u => u.FirstName).ThenBy(u => u.LastName),
            "-name" => rows.OrderByDescending(u => u.FirstName).ThenByDescending(u => u.LastName),
            "employee" => rows.OrderBy(u => u.EmployeeNo),
            "-employee" => rows.OrderByDescending(u => u.EmployeeNo),
            "role" => rows.OrderBy(u => u.Role).ThenBy(u => u.UserName),
            "-role" => rows.OrderByDescending(u => u.Role).ThenBy(u => u.UserName),
            _ => rows.OrderBy(u => u.UserName),
        };

        return (await PagedResult<ApplicationUser>.CreateAsync(ordered, query, ct)).Map(ToDto);
    }

    public async Task<UserDto?> GetAsync(long id)
    {
        var user = await users.FindByIdAsync(id.ToString());
        return user is null ? null : ToDto(user);
    }

    public async Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken ct)
    {
        var employeeNo = request.EmployeeNo.Trim();
        await EnsureEmployeeNoFreeAsync(employeeNo, excludeId: null, ct);

        var user = new ApplicationUser
        {
            UserName = request.UserName.Trim(),
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            EmployeeNo = employeeNo,
            Role = request.Role!.Value,
            IsActive = true, // created by an Admin, so usable at once
        };
        // CreateAsync checks the user name and runs the password validators (length, upper/lower case, digit, symbol).
        var result = await users.CreateAsync(user, request.Password);
        ThrowIfFailed(result, passwordField: "password");
        return ToDto(user);
    }

    /// <summary>Returns null when the id does not exist. The user name is not editable (it is the login name).</summary>
    public async Task<UserDto?> UpdateAsync(long id, UpdateUserRequest request, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(id.ToString());
        if (user is null) return null;

        var employeeNo = request.EmployeeNo.Trim();
        await EnsureEmployeeNoFreeAsync(employeeNo, excludeId: id, ct);

        var newRole = request.Role!.Value;
        if (user.Role == Role.ADMIN && newRole != Role.ADMIN)
            await EnsureAnotherActiveAdminAsync(user, "The last active Admin cannot be changed to another role.", ct);

        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        user.EmployeeNo = employeeNo;
        user.Role = newRole; // the role claim is rebuilt from the database on the user's next request
        ThrowIfFailed(await users.UpdateAsync(user), passwordField: "password");
        return ToDto(user);
    }

    /// <summary>
    /// Flips IsActive. Deactivating also changes the security stamp, so the user's next request is answered 401.
    /// Returns null when the id does not exist.
    /// </summary>
    public async Task<UserDto?> ToggleActiveAsync(long id, long actingUserId, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(id.ToString());
        if (user is null) return null;

        if (user.IsActive)
        {
            if (user.Id == actingUserId)
                throw Rule("self-deactivate", "You cannot deactivate your own account.");
            if (user.Role == Role.ADMIN)
                await EnsureAnotherActiveAdminAsync(user, "The last active Admin cannot be deactivated.", ct);

            user.IsActive = false;
            ThrowIfFailed(await users.UpdateAsync(user), passwordField: "password");
            await users.UpdateSecurityStampAsync(user); // invalidates all of this user's sign-in cookies
        }
        else
        {
            user.IsActive = true;
            ThrowIfFailed(await users.UpdateAsync(user), passwordField: "password");
        }
        return ToDto(user);
    }

    /// <summary>Admin sets a new password. The old one is never shown. Returns false when the id does not exist.</summary>
    public async Task<bool> ResetPasswordAsync(long id, string newPassword)
    {
        var user = await users.FindByIdAsync(id.ToString());
        if (user is null) return false;

        // Identity's reset flow: a one-time token proves "this reset is allowed", then the new password is validated and hashed.
        var token = await users.GeneratePasswordResetTokenAsync(user);
        ThrowIfFailed(await users.ResetPasswordAsync(user, token, newPassword), passwordField: "newPassword");
        return true; // ResetPasswordAsync also renews the security stamp, so old sessions of that user end
    }

    // ---- rules ----

    private async Task EnsureAnotherActiveAdminAsync(ApplicationUser target, string message, CancellationToken ct)
    {
        if (!target.IsActive) return; // an already inactive admin is not "the last active admin"
        var others = await users.Users.CountAsync(u => u.Role == Role.ADMIN && u.IsActive && u.Id != target.Id, ct);
        if (others == 0) throw Rule("last-admin", message);
    }

    private async Task EnsureEmployeeNoFreeAsync(string employeeNo, long? excludeId, CancellationToken ct)
    {
        var skip = excludeId ?? 0;
        if (await users.Users.AnyAsync(u => u.EmployeeNo == employeeNo && u.Id != skip, ct))
            throw Invalid("employeeNo", "This employee number is already used by another user.");
    }

    // ---- helpers ----

    private static void ThrowIfFailed(IdentityResult result, string passwordField)
    {
        if (result.Succeeded) return;
        // Identity error codes look like "PasswordTooShort", "DuplicateUserName", "InvalidUserName".
        var errors = result.Errors
            .GroupBy(e => e.Code.StartsWith("Password") ? passwordField : "userName")
            .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray());
        throw new DomainException("validation", result.Errors.First().Description) { Errors = errors };
    }

    private static DomainException Invalid(string field, string message) =>
        new("validation", message) { Errors = new Dictionary<string, string[]> { [field] = [message] } };

    private static DomainException Rule(string code, string message) => new(code, message);

    private static UserDto ToDto(ApplicationUser u) => new(
        u.Id, u.UserName ?? "", u.FirstName, u.LastName, u.EmployeeNo, u.Role.ToString(),
        u.IsActive, u.LastLoginAt, IsPending: !u.IsActive && u.LastLoginAt is null);
}
