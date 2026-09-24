using Auktionshuset.Api.Security;
using Microsoft.AspNetCore.Http.HttpResults;
using Auktionshuset.Contracts.Dto.Admin.Lot.UpdateLot;
using Auktionshuset.Application.Admin.Lots.UpdateLot;
using Microsoft.AspNetCore.Mvc;

namespace Auktionshuset.Api.Endpoints.Admin.Lot.UpdateLot
{
    public static class UpdateLotEndpoint
    {
        /// <summary>
        /// Maps the update-lot endpoint onto the supplied route group.
        /// </summary>
        /// <param name="group">The route group that the endpoint is mapped onto.</param>
        /// <returns>The same route group so that further endpoints can be chained.</returns>
        public static RouteGroupBuilder MapUpdateLot(this RouteGroupBuilder group)
        {
            group.MapPut("/{lotId:guid}", HandleAsync)
                .WithName("UpdateLot")
                .WithSummary("Updates a lot")
                .Produces<UpdateLotResponse>(StatusCodes.Status200OK)
                .Produces(StatusCodes.Status404NotFound)
                .ProducesValidationProblem()
                .RequireAuthorization(SecurityPolicies.CanUpdateLot);

            return group;
        }

        /// <summary>
        /// Trims and de-duplicates the request values and updates the lot identified by the route.
        /// </summary>
        /// <param name="lotId">The identifier of the lot to update.</param>
        /// <param name="request">The replacement values supplied by the client.</param>
        /// <param name="handler">The handler that updates the lot.</param>
        /// <returns>The updated lot identifier, or 404 when the lot does not exist.</returns>
        public static async Task<Results<Ok<UpdateLotResponse>, NotFound>> HandleAsync(
            [FromRoute] Guid lotId,
            [FromBody] UpdateLotRequest request,
            [FromServices] UpdateLotHandler handler,
            CancellationToken cancellationToken)
        {
            var command = new UpdateLotCommand(
                LotId: lotId,
                Name: request.Name.Trim(),
                Category: request.Category.Trim(),
                Quantity: request.Quantity,
                EstimatedValue: request.EstimatedValue,
                Description: request.Description.Trim(),
                Tags: request.Tags
                    .Select(tag => tag.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                AuctionHouseId: request.AuctionHouseId);

            UpdateLotResult? result = await handler.HandleAsync(command, cancellationToken);

            return result == null ? TypedResults.NotFound() : TypedResults.Ok(new UpdateLotResponse(result.LotId));
        }
    }
}
