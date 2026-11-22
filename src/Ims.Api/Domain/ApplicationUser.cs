using Microsoft.AspNetCore.Identity;

namespace Ims.Api.Domain;

/// <summary>
/// A person who can sign in. Inherits Identity's columns (UserName, PasswordHash, SecurityStamp...)
/// and adds our own. <c>long</c> is the primary-key type. Users are never hard-deleted (see IsActive).
/// </summary>
public class ApplicationUser : IdentityUser<long>
{
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string EmployeeNo { get; set; } = "";
    public Role Role { get; set; } = Role.REPORT_VIEWER;
    public bool IsActive { get; set; }

    public string DisplayName => $"{FirstName} {LastName}".Trim() is { Length: > 0 } n ? n : UserName ?? "";
}
