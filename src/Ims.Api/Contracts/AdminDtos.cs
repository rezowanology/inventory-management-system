using System.ComponentModel.DataAnnotations;
using Ims.Api.Domain;
using Ims.Api.Infrastructure;

namespace Ims.Api.Contracts;

// ---- Master data (products, components, points) ----

/// <summary>List filters for master data: ?status=active|deleted|all (default active) and, for points, ?type=.</summary>
public class MasterDataListQuery : ListQuery
{
    public string Status { get; set; } = "active";
    public PointType? Type { get; set; }
}

/// <summary>
/// Body of create/update. DataAnnotations ([Required], [StringLength]) are checked automatically by [ApiController];
/// a bad body answers 400 with a field-error list before the action even runs.
/// <c>Type</c> is only used by points (TRACK / RECEIVE / DELIVERY).
/// </summary>
public record MasterDataRequest(
    [Required, StringLength(100, MinimumLength = 2)] string Name,
    PointType? Type);

public record MasterDataDto(
    long Id, string Name, bool IsDeleted, DateTime? DeletedAt, DateTime CreatedAt, DateTime UpdatedAt, string? PointType);

/// <summary>One dropdown entry (only non-deleted rows are ever offered).</summary>
public record OptionDto(long Id, string Name);

/// <summary>Result of DELETE: Archived = true means "in use, so it was soft-deleted and can be restored".</summary>
public record DeleteResultDto(bool Archived);

// ---- Users ----

public class UserListQuery : ListQuery
{
    public Role? Role { get; set; }
    public bool? Active { get; set; }
    /// <summary>Pending registration = inactive AND never logged in.</summary>
    public bool? Pending { get; set; }
}

public record UserDto(
    long Id, string UserName, string FirstName, string LastName, string EmployeeNo,
    string Role, bool IsActive, DateTime? LastLoginAt, bool IsPending);

public record CreateUserRequest(
    [Required, StringLength(150, MinimumLength = 3)] string UserName,
    [Required, StringLength(150)] string FirstName,
    [Required, StringLength(150)] string LastName,
    [Required, StringLength(20, MinimumLength = 2)] string EmployeeNo,
    [Required] Role? Role, // nullable + [Required]: a missing role must not silently become ADMIN (the enum's zero value)
    [Required] string Password);

public record UpdateUserRequest(
    [Required, StringLength(150)] string FirstName,
    [Required, StringLength(150)] string LastName,
    [Required, StringLength(20, MinimumLength = 2)] string EmployeeNo,
    [Required] Role? Role);

public record ResetPasswordRequest([Required] string NewPassword);

public record ChangePasswordRequest(
    [Required] string CurrentPassword,
    [Required] string NewPassword,
    [Required] string ConfirmPassword);
