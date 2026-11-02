using System.Security.Claims;
using Auktionshuset.Contracts.Security;

namespace Auktionshuset.Security;

/// <summary>
/// Chooses the landing page from server-validated roles: customers go to their own
/// area, administrative roles to the existing administration, and users without a
/// known role are denied access.
/// </summary>
public static class AuthDestination
{
    public const string CustomerHome = "/kunde";
    public const string Administration = "/";

    public static string? For(ClaimsPrincipal user) =>
        user.IsInRole(RoleNames.Customer) ? CustomerHome
        : user.FindFirst(ClaimTypes.Role) is not null ? Administration
        : null;
}
