using Auktionshuset.Application.Abstraction.Auction;
using Auktionshuset.Application.Admin.Auctions.CloseAuction;
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
            try
            {
                Guid? winningBidId = await handler.HandleAsync(auctionLotId, cancellationToken);
                return Results.Ok(new { WinningBidId = winningBidId });
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
            catch (AuctionStateConflictException exception)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    detail: exception.Message
                );
            }
        }
    }
}
