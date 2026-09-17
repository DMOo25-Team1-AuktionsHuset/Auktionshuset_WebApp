namespace Auktionshuset.Api.Endpoints.Admin.CreateAuction
{
    public static class AuctionEndpoints
    {
        public static IEndpointRouteBuilder MapAuctionEndpoints(this IEndpointRouteBuilder endpoints)
        {
            var group = endpoints
                .MapGroup("/api/auctions")
                .WithTags("Auctions");

            group.MapCreateAuction();

            return endpoints;
        }
    }
}
