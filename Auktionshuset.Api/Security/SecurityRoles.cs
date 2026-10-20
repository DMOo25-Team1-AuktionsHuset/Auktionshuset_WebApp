namespace Auktionshuset.Api.Security
{
    public static class SecurityRoles
    {
        public const string Admin = Auktionshuset.Contracts.Security.RoleNames.Admin;
        public const string LotAdmin = Auktionshuset.Contracts.Security.RoleNames.LotAdmin;
        public const string AuctionAdmin = Auktionshuset.Contracts.Security.RoleNames.AuctionAdmin;
        public const string Employee = Auktionshuset.Contracts.Security.RoleNames.Employee;
        public const string Customer = Auktionshuset.Contracts.Security.RoleNames.Customer;
    }
}
