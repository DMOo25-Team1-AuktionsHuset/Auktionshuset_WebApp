using Auktionshuset.Api.Security;
using Auktionshuset.Application.Abstraction.Auction;

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
