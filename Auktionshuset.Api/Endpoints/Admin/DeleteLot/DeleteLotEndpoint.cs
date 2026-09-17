using Auktionshuset.Application.Admin.Lots.DeleteLot;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Auktionshuset.Api.Endpoints.Admin.DeleteLot;

public static class DeleteLotEndpoint
{
    /// <summary>
    /// Maps the delete-lot endpoint onto the supplied route group.
    /// </summary>
    /// <param name="group">The route group that the endpoint is mapped onto.</param>
    /// <returns>The same route group so that further endpoints can be chained.</returns>
    public static RouteGroupBuilder MapDeleteLot(this RouteGroupBuilder group)
    {
        group.MapDelete("/{lotId:guid}", HandleAsync)
            .WithName("DeleteLot")
            .WithSummary("Deletes an auction lot")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .RequireAuthorization("CanDeleteLot");
        return group;
    }

    /// <summary>
    /// Deletes the lot with the given route identifier.
    /// </summary>
    /// <param name="lotId">The identifier of the lot to delete.</param>
    /// <param name="handler">The handler that deletes the lot.</param>
    /// <returns>204 when the lot was deleted, otherwise 404.</returns>
    public static async Task<Results<NoContent, NotFound>> HandleAsync(
        [FromRoute] Guid lotId,
        [FromServices] DeleteLotHandler handler,
        CancellationToken cancellationToken)
    {
        var deleted = await handler.HandleAsync(new DeleteLotCommand(lotId), cancellationToken);
        return deleted ? TypedResults.NoContent() : TypedResults.NotFound();
    }
}
