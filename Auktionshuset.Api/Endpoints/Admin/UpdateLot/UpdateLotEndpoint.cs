using Auktionshuset.Application.Admin.Lots;
using Microsoft.AspNetCore.Http.HttpResults;
using Auktionshuset.Contracts.Dto.Admin.Lot.UpdateLot;
using Auktionshuset.Application.Admin.Lots.UpdateLot;

namespace Auktionshuset.Api.Endpoints.Admin.UpdateLot {
    public static class UpdateLotEndpoint {
        public static RouteGroupBuilder MapUpdateLot(this RouteGroupBuilder group) {
            group.MapPut("/{lotId:guid}", HandleAsync)
                .WithName("UpdateLot")
                .WithSummary("Updates a lot")
                .Produces<UpdateLotResponse>(StatusCodes.Status200OK)
                .Produces(StatusCodes.Status404NotFound)
                .ProducesValidationProblem();

            return group;
        }

        public static async Task<Results<Ok<UpdateLotResponse>, NotFound>> HandleAsync(
            Guid lotId, 
            UpdateLotRequest request, 
            UpdateLotHandler handler, 
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

            var result = await handler.HandleAsync(command, cancellationToken);

            return result == null ? TypedResults.NotFound() : TypedResults.Ok(new UpdateLotResponse(result.LotId));
        }
    }
}
