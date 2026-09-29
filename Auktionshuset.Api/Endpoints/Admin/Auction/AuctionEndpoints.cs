using Auktionshuset.Api.Endpoints.Admin.Auction.CloseAuction;
using Auktionshuset.Api.Endpoints.Admin.Auction.CreateAuction;
using Auktionshuset.Api.Endpoints.Admin.Auction.DeleteAuction;
using Auktionshuset.Api.Endpoints.Admin.Auction.GetAuctions;
using Auktionshuset.Api.Endpoints.Admin.Auction.UpdateAuction;
using Auktionshuset.Api.Security;

namespace Auktionshuset.Api.Endpoints.Admin.Auction
{
    public static class AuctionEndpoints
    {
        /// <summary>
        /// Maps every auction endpoint under the <c>/api/auctions</c> route group.
        /// </summary>
        /// <param name="endpoints">The endpoint route builder that the route group is added to.</param>
        /// <returns>The same endpoint route builder so that further routes can be mapped.</returns>
        public static IEndpointRouteBuilder MapAuctionEndpoints(this IEndpointRouteBuilder endpoints)
        {
            RouteGroupBuilder group = endpoints
                .MapGroup("/api/auctions")
                .WithTags("Auctions")
                .RequireAuthorization(SecurityPolicies.CanCreateAuction);

            group.MapGetAuctions();
            group.MapCreateAuction();
            group.MapUpdateAuction();
            group.MapDeleteAuction();
            group.MapCloseAuctionLotEndpoint();

            return endpoints;
        }
    }
}
