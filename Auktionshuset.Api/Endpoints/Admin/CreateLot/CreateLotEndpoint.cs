using Auktionshuset.Application.Abstraction.Admin.Lots;
using Auktionshuset.Contracts.Dto.Admin.Lot;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Auktionshuset.Api.Endpoints.Admin.CreateLot {
    public static class CreateLotEndpoint {
        public static RouteGroupBuilder MapCreateLot(this RouteGroupBuilder group) {
            group.MapPost("/", HandleAsync)
                .WithName("CreateLot")
                .WithSummary("Creates a new auction lot")
                .Produces<CreateLotResponse>(StatusCodes.Status201Created)
                .ProducesValidationProblem();
                //.ProducesProblem(StatusCodes.Status404NotFound)
                //.RequireAuthorization("CanCreateLot");
            return group;
        }

        public static async Task<Created<CreateLotResponse>> HandleAsync(CreateLotRequest request, 
            CreateLotHandler handler, 
            CancellationToken cancellationToken) {
            var command = new CreateLotCommand(
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

            return TypedResults.Created($"/api/lot/{result.LotId}", new CreateLotResponse(result.LotId));
        }
    }
}
