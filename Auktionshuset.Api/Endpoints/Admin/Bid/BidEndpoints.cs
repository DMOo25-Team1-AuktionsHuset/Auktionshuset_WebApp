using Auktionshuset.Application.Abstraction.Auction;
using Auktionshuset.Application.Auction;
using Auktionshuset.Contracts.Dto.Auction;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Auktionshuset.Infrastructure.Database;

namespace Auktionshuset.Api.Endpoints.Admin.Bid
{
    public static class BidEndpoints
    {
        public static IEndpointRouteBuilder MapBidEndpoints(this IEndpointRouteBuilder group)
        {
            group.MapPost("/api/bids/{auctionLotId:guid}", HandleAsync)
                .WithName("Bid")
                .WithSummary("Creates a new bid")
                .RequireAuthorization(Auktionshuset.Api.Security.SecurityPolicies.Customer)
                .WithTags("Bidding");

            return group;
        }

        public static async Task<IResult> HandleAsync(
            [FromRoute] Guid auctionLotId,
            [FromBody] PlaceBidRequest request,
            ClaimsPrincipal user,
            [FromServices] PlaceBidHandler handler,
            [FromServices] Auktionshuset.Api.Services.CustomerProfileService profiles,
            [FromServices] AHDBContext db,
            CancellationToken cancellationToken)
        {
            var own = await profiles.GetOwnProfileAsync(user, cancellationToken);
            if (own.Profile is null)
            {
                return Results.Problem(Auktionshuset.Api.Endpoints.Customer.CustomerProfileProblems.NoCustomerLink());
            }
            Guid customerId = own.Profile.CustomerId;
            Guid? auctionId = await db.AuctionLot.Where(l => l.AuctionLotId == auctionLotId)
                .Select(l => (Guid?)l.AuctionId).SingleOrDefaultAsync(cancellationToken);
            if (auctionId is null) return Results.NotFound();
            if (!await db.AuctionRegistrations.AnyAsync(r => r.AuctionId == auctionId
                && r.CustomerId == customerId, cancellationToken))
                return Results.Problem(statusCode: 403, detail: "Du er ikke tilmeldt denne auktion.");

            Guid? deviceSessionId = null;

            PlaceBidResult result = await handler.HandleAsync(
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
                "NotRegistered" => Results.Problem(statusCode: 403, detail: "Du er ikke tilmeldt denne auktion."),
                _ => Results.Conflict(result)
            };
        }
    }
}
