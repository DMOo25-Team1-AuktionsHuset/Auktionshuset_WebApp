using Auktionshuset.Application.Admin.Auctions.CreateAuction;
using Auktionshuset.Contracts.Dto.Admin.Auction;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Auktionshuset.Api.Endpoints.Admin.CreateAuction
{
    public static class CreateAuctionEndpoint
    {
        public static RouteGroupBuilder MapCreateAuction(this RouteGroupBuilder group)
        {
            group.MapPost("/", HandleAsync)
                .WithName("CreateAuction")
                .WithSummary("Creates a new auction")
                .Produces<CreateAuctionResponse>(StatusCodes.Status201Created)
                .ProducesValidationProblem();

            return group;
        }

        private static async Task<Results<Created<CreateAuctionResponse>, ValidationProblem>> HandleAsync(
            CreateAuctionRequest request,
            CreateAuctionHandler handler,
            CancellationToken cancellationToken)
        {
            var command = new CreateAuctionCommand(
                StartsAt: request.StartsAt!.Value,
                LotIds: request.LotIds ?? []);

            var result = await handler.HandleAsync(command, cancellationToken);

            if (!result.Succeeded)
            {
                var errors = result.Errors
                    .Select((message, index) => (Key: $"request[{index}]", Messages: new[] { message }))
                    .ToDictionary(error => error.Key, error => error.Messages);

                return TypedResults.ValidationProblem(errors);
            }

            var response = new CreateAuctionResponse(result.AuctionId, result.LotCount);

            return TypedResults.Created($"/api/auctions/{result.AuctionId}", response);
        }
    }
}
