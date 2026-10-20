namespace Auktionshuset.Security;

public static class FrontendPolicies
{
    public const string Admin = "Admin";
    public const string CanViewLots = "CanViewLots";
    public const string CanCreateLot = "CanCreateLot";
    public const string CanUpdateLot = "CanUpdateLot";
    public const string CanDeleteLot = "CanDeleteLot";
    public const string CanViewAuctions = "CanViewAuctions";
    public const string CanCreateAuction = "CanCreateAuction";
    public const string CanUpdateAuction = "CanUpdateAuction";
    public const string CanDeleteAuction = "CanDeleteAuction";
    public const string CanViewEmployees = "CanViewEmployees";

    public const string PermissionClaimType = "permission";
    public const string ViewLotsPermission = Auktionshuset.Contracts.Security.PermissionNames.LotsRead;
    public const string CreateLotPermission = Auktionshuset.Contracts.Security.PermissionNames.LotsCreate;
    public const string UpdateLotPermission = Auktionshuset.Contracts.Security.PermissionNames.LotsUpdate;
    public const string DeleteLotPermission = Auktionshuset.Contracts.Security.PermissionNames.LotsDelete;
    public const string ViewAuctionsPermission = Auktionshuset.Contracts.Security.PermissionNames.AuctionsRead;
    public const string CreateAuctionPermission = Auktionshuset.Contracts.Security.PermissionNames.AuctionsCreate;
    public const string UpdateAuctionPermission = Auktionshuset.Contracts.Security.PermissionNames.AuctionsUpdate;
    public const string DeleteAuctionPermission = Auktionshuset.Contracts.Security.PermissionNames.AuctionsDelete;
    public const string ViewEmployeesPermission = Auktionshuset.Contracts.Security.PermissionNames.EmployeesRead;
}
