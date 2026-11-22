using System.Security.Claims;
using Ims.Api.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Ims.Api.Authorization;

/// <summary>
/// Decides which claims go into the sign-in cookie. We add the user's single role as a standard Role claim
/// (that is what policies check) plus "employee_no".
/// </summary>
public class ImsClaimsPrincipalFactory(UserManager<ApplicationUser> userManager, IOptions<IdentityOptions> options)
    : UserClaimsPrincipalFactory<ApplicationUser>(userManager, options)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        identity.AddClaim(new Claim(ClaimTypes.Role, user.Role.ToString()));
        identity.AddClaim(new Claim("employee_no", user.EmployeeNo));
        return identity;
    }
}
