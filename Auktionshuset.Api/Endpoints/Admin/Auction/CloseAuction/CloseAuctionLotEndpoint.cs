using Auktionshuset.Application.Admin.Auctions.CloseAuction;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Auktionshuset.Api.Endpoints.Admin.Auction.CloseAuction
{
    public static class CloseAuctionLotEndpoint
    {
        public static IEndpointRouteBuilder MapCloseAuctionLotEndpoint(this IEndpointRouteBuilder group)
        {
            group.MapPost("/api/auction-lots/{auctionLotId:guid}", HandleAsync)
                .AllowAnonymous()
                .WithTags("Bidding");

            return group;
        }

        public static async Task<IResult> HandleAsync(
            [FromRoute] Guid auctionLotId,
            [FromServices] CloseAuctionLotHandler handler,
            CancellationToken cancellationToken)
        {
            Guid? winningBidId = await handler.HandleAsync(auctionLotId, cancellationToken);

            return winningBidId == null
                ? Results.Ok(new { WinningBidId = (Guid?)null })
                : Results.Ok(new { WinningBidId = winningBidId });
        }
    }
}
