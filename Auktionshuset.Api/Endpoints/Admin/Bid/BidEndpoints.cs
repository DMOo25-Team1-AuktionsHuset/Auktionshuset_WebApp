using Auktionshuset.Application.Abstraction.Auction;
using Auktionshuset.Application.Auction;
using Auktionshuset.Contracts.Dto.Auction;
using Auktionshuset.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Auktionshuset.Api.Endpoints.Admin.Bid
{
    public static class BidEndpoints
    {
        public static IEndpointRouteBuilder MapBidEndpoints(this IEndpointRouteBuilder group)
        {
            group.MapPost("/api/bids/{auctionLotId:guid}", HandleAsync)
                .WithTags("Bidding");

            return group;
        }

        public static async Task<IResult> HandleAsync(
            [FromRoute] Guid auctionLotId,
            [FromBody] PlaceBidRequest request,
            ClaimsPrincipal user,
            [FromServices] PlaceBidHandler handler,
            CancellationToken cancellationToken)
        {
            if (!Guid.TryParse(
                user.FindFirst("customer_id")?.Value,
                out var customerId))
            {
                return Results.Unauthorized();
            }

            Guid? deviceSessionId = null;

            var result = await handler.HandleAsync(
                new PlaceBidCommand(
                    AuctionLotId: auctionLotId,
                    CustomerId: customerId,
                    DeviceSessionId: deviceSessionId,
                    RequestId: request.RequestId,
                    Amount: request.Amount),
                cancellationToken);

            if (result.Accepted)
            {
                return Results.Ok(result);
            }

            return result.ErrorCode switch
            {
                "AuctionItemNotFound" => Results.NotFound(result),
                "InvalidCustomer" => Results.Unauthorized(),
                "InvalidDeviceSession" => Results.Unauthorized(),
                _ => Results.Conflict(result)
            };
        }
    }
}
