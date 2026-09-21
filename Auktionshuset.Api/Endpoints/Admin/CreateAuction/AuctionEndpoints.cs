using Auktionshuset.Api.Endpoints.Admin.DeleteAuction;
using Auktionshuset.Api.Endpoints.Admin.GetAuctions;
using Auktionshuset.Api.Endpoints.Admin.UpdateAuction;

namespace Auktionshuset.Api.Endpoints.Admin.CreateAuction
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
            var group = endpoints
                .MapGroup("/api/auctions")
                .WithTags("Auctions");

            group.MapGetAuctions();
            group.MapCreateAuction();
            group.MapUpdateAuction();
            group.MapDeleteAuction();

            return endpoints;
        }
    }
}
