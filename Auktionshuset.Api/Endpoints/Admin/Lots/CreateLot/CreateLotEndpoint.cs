using Auktionshuset.Application.Abstraction.Admin.Lots;
using Auktionshuset.Api.Security;
using Auktionshuset.Application.Admin.Lots.CreateLot;
using Auktionshuset.Contracts.Dto.Admin.Lot.CreateLot;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Auktionshuset.Api.Endpoints.Admin.Lot.CreateLot {
    public static class CreateLotEndpoint {
        /// <summary>
        /// Maps the create-lot endpoint onto the supplied route group.
        /// </summary>
        /// <param name="group">The route group that the endpoint is mapped onto.</param>
        /// <returns>The same route group so that further endpoints can be chained.</returns>
        public static RouteGroupBuilder MapCreateLot(this RouteGroupBuilder group) {
            group.MapPost("/", HandleAsync)
                .WithName("CreateLot")
                .WithSummary("Creates a new auction lot")
                .Produces<CreateLotResponse>(StatusCodes.Status201Created)
                .ProducesValidationProblem()
                .RequireAuthorization(SecurityPolicies.CanCreateLot);
            return group;
        }

        /// <summary>
        /// Trims and de-duplicates the request values, creates the lot through the handler and
        /// returns the new lot's location.
        /// </summary>
        /// <param name="request">The lot data supplied by the client.</param>
        /// <param name="handler">The handler that creates the lot.</param>
        /// <returns>A 201 response carrying the new lot identifier.</returns>
        public static async Task<Created<CreateLotResponse>> HandleAsync(
            [FromBody]CreateLotRequest request, 
            [FromServices]CreateLotHandler handler, 
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

            return TypedResults.Created($"/api/lots/{result.LotId}", new CreateLotResponse(result.LotId));
        }
    }
}

