namespace Auktionshuset.Api.Security;

public static class SecurityRolePermissions
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyCollection<string>> PermissionsByRole =
        new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal)
        {
            [SecurityRoles.LotAdmin] =
            [
                SecurityPermissions.ViewLots,
                SecurityPermissions.CreateLot,
                SecurityPermissions.UpdateLot,
                SecurityPermissions.DeleteLot
            ],
            [SecurityRoles.AuctionAdmin] =
            [
                SecurityPermissions.ViewLots,
                SecurityPermissions.CreateLot,
                SecurityPermissions.UpdateLot,
                SecurityPermissions.DeleteLot,
                SecurityPermissions.ViewAuctions,
                SecurityPermissions.CreateAuction,
                SecurityPermissions.UpdateAuction,
                SecurityPermissions.DeleteAuction
            ],
            [SecurityRoles.Admin] = SecurityPermissions.All,
            [SecurityRoles.Employee] = [],
            [SecurityRoles.Customer] = []
        };

    public static IReadOnlyCollection<string> GetPermissions(string role) =>
        PermissionsByRole.TryGetValue(role, out IReadOnlyCollection<string>? permissions)
            ? permissions
            : throw new InvalidOperationException($"Authentication configuration contains unknown role '{role}'.");

    public static bool IsKnownRole(string role) => PermissionsByRole.ContainsKey(role);
}
