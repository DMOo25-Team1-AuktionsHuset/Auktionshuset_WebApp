namespace Auktionshuset.Api.Security;

public static class SecurityPolicies
{
    public const string Admin = nameof(Admin);
    public const string CanReadEmployees = nameof(CanReadEmployees);
    public const string CanCreateLot = nameof(CanCreateLot);
    public const string CanUpdateLot = nameof(CanUpdateLot);
    public const string CanDeleteLot = nameof(CanDeleteLot);
    public const string CanViewLots = nameof(CanViewLots);
    public const string CanViewAuctions = nameof(CanViewAuctions);
    public const string CanCreateAuction = nameof(CanCreateAuction);
    public const string CanUpdateAuction = nameof(CanUpdateAuction);
    public const string CanDeleteAuction = nameof(CanDeleteAuction);
    public const string CanViewEmployeeOptions = nameof(CanViewEmployeeOptions);
    public const string CanViewEmployees = nameof(CanViewEmployees);
    public const string CanCreateEmployee = nameof(CanCreateEmployee);
    public const string CanUpdateEmployee = nameof(CanUpdateEmployee);
    public const string CanDeleteEmployee = nameof(CanDeleteEmployee);
    public const string CanWriteLotImage = nameof(CanWriteLotImage);
}

public static class SecurityPermissions
{
    public const string ClaimType = "permission";
    public const string CreateLot = Auktionshuset.Contracts.Security.PermissionNames.LotsCreate;
    public const string UpdateLot = Auktionshuset.Contracts.Security.PermissionNames.LotsUpdate;
    public const string DeleteLot = Auktionshuset.Contracts.Security.PermissionNames.LotsDelete;
    public const string ViewLots = Auktionshuset.Contracts.Security.PermissionNames.LotsRead;
    public const string ViewAuctions = Auktionshuset.Contracts.Security.PermissionNames.AuctionsRead;
    public const string CreateAuction = Auktionshuset.Contracts.Security.PermissionNames.AuctionsCreate;
    public const string UpdateAuction = Auktionshuset.Contracts.Security.PermissionNames.AuctionsUpdate;
    public const string DeleteAuction = Auktionshuset.Contracts.Security.PermissionNames.AuctionsDelete;
    public const string ViewEmployees = Auktionshuset.Contracts.Security.PermissionNames.EmployeesRead;
    public const string CreateEmployee = Auktionshuset.Contracts.Security.PermissionNames.EmployeesCreate;
    public const string UpdateEmployee = Auktionshuset.Contracts.Security.PermissionNames.EmployeesUpdate;
    public const string DeleteEmployee = Auktionshuset.Contracts.Security.PermissionNames.EmployeesDelete;

    public static readonly IReadOnlyCollection<string> All =
        [
            CreateLot,
            UpdateLot,
            DeleteLot,
            ViewLots,
            ViewAuctions,
            CreateAuction,
            UpdateAuction,
            DeleteAuction,
            ViewEmployees,
            CreateEmployee,
            UpdateEmployee,
            DeleteEmployee
        ];
}
