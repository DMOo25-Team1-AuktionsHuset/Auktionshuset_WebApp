namespace Auktionshuset.Api.Security
{
    public static class SecurityRoles
    {
        public const string AuctionAdmin = Auktionshuset.Contracts.Security.AdminRoles.AuctionAdmin;
        public const string LotAdmin = Auktionshuset.Contracts.Security.AdminRoles.LotAdmin;
        public const string SuperAdmin = Auktionshuset.Contracts.Security.AdminRoles.SuperAdmin;
        public const string Employee = nameof(Employee);
        public const string Customer = nameof(Customer);
    }
}
