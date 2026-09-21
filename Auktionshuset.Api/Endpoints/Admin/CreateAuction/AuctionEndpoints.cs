using Auktionshuset.Api.Security;

namespace Auktionshuset.Api.Endpoints.Admin.CreateAuction
{
    public static class AuctionEndpoints
    {
        public static IEndpointRouteBuilder MapAuctionEndpoints(this IEndpointRouteBuilder endpoints)
        {
            var group = endpoints
                .MapGroup("/api/auctions")
                .WithTags("Auctions")
                .RequireAuthorization(SecurityPolicies.CanCreateAuction);

            group.MapCreateAuction();

            return endpoints;
        }
    }
}
