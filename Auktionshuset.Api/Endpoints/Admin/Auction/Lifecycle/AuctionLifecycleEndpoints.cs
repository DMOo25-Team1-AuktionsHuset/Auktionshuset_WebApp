using Auktionshuset.Api.Security;
using Auktionshuset.Application.Abstraction.Auction;
using Auktionshuset.Contracts.Dto.Admin.Auction;
using Microsoft.AspNetCore.Mvc;

namespace Auktionshuset.Api.Endpoints.Admin.Auction.Lifecycle
{
    public static class AuctionLifecycleEndpoints
    {
        public static RouteGroupBuilder MapAuctionLifecycle(this RouteGroupBuilder group)
        {
            group.MapPost("/{auctionId:guid}/start",
                    (
                            Guid auctionId,
                            IAuctionLifeCycleStore lifecycleStore,
                            CancellationToken cancellationToken) =>
                        ExecuteAsync(() =>
                            lifecycleStore.StartAsync(auctionId, cancellationToken)))
                .RequireAuthorization(SecurityPolicies.CanUpdateAuction);

            group.MapPost("/{auctionId:guid}/close",
                    (
                            Guid auctionId,
                            IAuctionLifeCycleStore lifecycleStore,
                            CancellationToken cancellationToken) =>
                        ExecuteAsync(() =>
                            lifecycleStore.CloseAsync(auctionId, cancellationToken)))
                .RequireAuthorization(SecurityPolicies.CanUpdateAuction);

            group.MapGet("/{auctionId:guid}/shared-lot", async (Guid auctionId, [FromServices] ISharedAuctionLotStore store, CancellationToken ct) =>
            {
                var state = await store.GetAsync(auctionId, ct);
                return state is null ? Results.NotFound() : Results.Ok(new SharedAuctionLotResponse(state.AuctionId, state.AuctionLotId, state.AuctionRevision));
            }).RequireAuthorization(SecurityPolicies.CanViewAuctions);

            group.MapPut("/{auctionId:guid}/shared-lot", async (Guid auctionId, SelectSharedAuctionLotRequest request, [FromServices] ISharedAuctionLotStore store, CancellationToken ct) =>
            {
                try
                {
                    var state = await store.SelectAsync(auctionId, request.AuctionLotId, ct);
                    return state is null ? Results.NotFound() : Results.Ok(new SharedAuctionLotResponse(state.AuctionId, state.AuctionLotId, state.AuctionRevision));
                }
                catch (AuctionStateConflictException exception)
                {
                    return Results.Problem(statusCode: StatusCodes.Status409Conflict, detail: exception.Message);
                }
            }).RequireAuthorization(SecurityPolicies.CanUpdateAuction);

            return group;
        }

        private static async Task<IResult> ExecuteAsync(Func<Task<bool>> operation)
        {
            try
            {
                return await operation()
                    ? Results.NoContent()
                    : Results.NotFound();
            }
            catch (AuctionStateConflictException exception)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    detail: exception.Message);
            }
        }
    }
}
