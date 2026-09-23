namespace Auktionshuset.Api.Security;

public static class SecurityPolicies
{
    public const string Admin = nameof(Admin);
    public const string CanCreateLot = nameof(CanCreateLot);
    public const string CanUpdateLot = nameof(CanUpdateLot);
    public const string CanDeleteLot = nameof(CanDeleteLot);
    public const string CanViewLots = nameof(CanViewLots);
    public const string CanCreateAuction = nameof(CanCreateAuction);
}

public static class SecurityPermissions
{
    public const string ClaimType = "permission";
    public const string CreateLot = "lots.create";
    public const string UpdateLot = "lots.update";
    public const string DeleteLot = "lots.delete";
    public const string ViewLots = "lots.read";
    public const string CreateAuction = "auctions.create";

    public static readonly IReadOnlyCollection<string> All =
        [
            CreateLot,
            UpdateLot,
            DeleteLot,
            ViewLots,
            CreateAuction
        ];
}
