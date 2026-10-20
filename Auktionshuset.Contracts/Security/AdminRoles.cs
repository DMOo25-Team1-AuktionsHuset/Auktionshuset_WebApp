using System.Security.Claims;
namespace Auktionshuset.Contracts.Security;

public static class AdminRoles
{
    public const string AuctionAdmin = nameof(AuctionAdmin);
    public const string LotAdmin = nameof(LotAdmin);
    public const string SuperAdmin = nameof(SuperAdmin);
    public static bool CanManageAuctions(ClaimsPrincipal user) =>
        user.IsInRole(AuctionAdmin) || user.IsInRole(SuperAdmin);
    public static bool CanManageLots(ClaimsPrincipal user) =>
        user.IsInRole(LotAdmin) || user.IsInRole(SuperAdmin);
    public static bool CanReadLots(ClaimsPrincipal user) =>
        CanManageLots(user) || CanManageAuctions(user);
}
