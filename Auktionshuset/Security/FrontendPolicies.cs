namespace Auktionshuset.Security;

public static class FrontendPolicies
{
    public const string Admin = "Admin";
    public const string CanViewLots = "CanViewLots";
    public const string CanCreateLot = "CanCreateLot";
    public const string CanUpdateLot = "CanUpdateLot";
    public const string CanDeleteLot = "CanDeleteLot";
    public const string CanCreateAuction = "CanCreateAuction";

    public const string PermissionClaimType = "permission";
    public const string ViewLotsPermission = "lots.read";
    public const string CreateLotPermission = "lots.create";
    public const string UpdateLotPermission = "lots.update";
    public const string DeleteLotPermission = "lots.delete";
    public const string CreateAuctionPermission = "auctions.create";
}
