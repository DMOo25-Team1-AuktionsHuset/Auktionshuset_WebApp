using Auktionshuset.Application.Admin.Auctions.DeleteAuction;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Auktionshuset.Api.Endpoints.Admin.DeleteAuction;

public static class DeleteAuctionEndpoint
{
    /// <summary>
    /// Maps the delete-auction endpoint onto the supplied route group.
    /// </summary>
    /// <param name="group">The route group that the endpoint is mapped onto.</param>
    /// <returns>The same route group so that further endpoints can be chained.</returns>
    public static RouteGroupBuilder MapDeleteAuction(this RouteGroupBuilder group)
    {
        group.MapDelete("/{auctionId:guid}", HandleAsync)
            .WithName("DeleteAuction")
            .WithSummary("Deletes an auction")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        return group;
    }

    /// <summary>
    /// Deletes the auction with the given route identifier.
    /// </summary>
    /// <param name="auctionId">The identifier of the auction to delete.</param>
    /// <param name="handler">The handler that deletes the auction.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>204 when the auction was deleted, otherwise 404.</returns>
    public static async Task<Results<NoContent, NotFound>> HandleAsync(
        Guid auctionId,
        DeleteAuctionHandler handler,
        CancellationToken cancellationToken)
    {
        bool deleted = await handler.HandleAsync(new DeleteAuctionCommand(auctionId), cancellationToken);

        return deleted ? TypedResults.NoContent() : TypedResults.NotFound();
    }
}
