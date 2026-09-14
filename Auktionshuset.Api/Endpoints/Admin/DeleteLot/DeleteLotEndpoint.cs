using Auktionshuset.Application.Admin.Lots.DeleteLot;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Auktionshuset.Api.Endpoints.Admin.DeleteLot;

public static class DeleteLotEndpoint
{
    public static RouteGroupBuilder MapDeleteLot(this RouteGroupBuilder group)
    {
        group.MapDelete("/{lotId:guid}", HandleAsync)
            .WithName("DeleteLot")
            .WithSummary("Deletes an auction lot")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        return group;
    }

    public static async Task<Results<NoContent, NotFound>> HandleAsync(
        [FromRoute] Guid lotId,
        [FromServices] DeleteLotHandler handler,
        CancellationToken cancellationToken)
    {
        var deleted = await handler.HandleAsync(new DeleteLotCommand(lotId), cancellationToken);
        return deleted ? TypedResults.NoContent() : TypedResults.NotFound();
    }
}
