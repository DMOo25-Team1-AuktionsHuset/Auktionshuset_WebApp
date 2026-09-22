using Auktionshuset.Application.Admin.Auctions;
using Auktionshuset.Application.Admin.Auctions.CreateAuction;
using Auktionshuset.Contracts.Dto.Admin.Auction;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Auktionshuset.Api.Endpoints.Admin.CreateAuction
{
    public static class CreateAuctionEndpoint
    {
        /// <summary>
        /// Maps the create-auction endpoint onto the supplied route group.
        /// </summary>
        /// <param name="group">The route group that the endpoint is mapped onto.</param>
        /// <returns>The same route group so that further endpoints can be chained.</returns>
        public static RouteGroupBuilder MapCreateAuction(this RouteGroupBuilder group)
        {
            group.MapPost("/", HandleAsync)
                .WithName("CreateAuction")
                .WithSummary("Creates a new auction")
                .Produces<CreateAuctionResponse>(StatusCodes.Status201Created)
                .ProducesValidationProblem();

            return group;
        }

        /// <summary>
        /// Creates an auction from the supplied request.
        /// </summary>
        /// <param name="request">The auction values and lot selections.</param>
        /// <param name="handler">The handler that creates the auction.</param>
        /// <param name="cancellationToken">The token used to cancel the operation.</param>
        /// <returns>The created auction response or a validation problem.</returns>
        public static async Task<Results<Created<CreateAuctionResponse>, ValidationProblem>> HandleAsync(
            [FromBody]CreateAuctionRequest request,
            [FromServices]CreateAuctionHandler handler,
            CancellationToken cancellationToken)
        {
            var command = new CreateAuctionCommand(
                Name: request.Name.Trim(),
                StartsAt: request.StartsAt!.Value,
                EndsAt: request.EndsAt!.Value,
                EmployeeId: request.EmployeeId,
                AuctionHouseId: request.AuctionHouseId,
                Lots: AuctionEndpointMapping.ToSelections(request.Lots));

            var result = await handler.HandleAsync(command, cancellationToken);

            if (!result.Succeeded)
            {
                return TypedResults.ValidationProblem(AuctionEndpointMapping.ToValidationErrors(result.Errors));
            }

            var response = new CreateAuctionResponse(result.AuctionId, result.LotCount, result.ItemCount);

            return TypedResults.Created($"/api/auctions/{result.AuctionId}", response);
        }
    }
}
