using Auktionshuset.Api.Endpoints.Admin.CreateAuction;
using Auktionshuset.Application.Admin.Auctions.UpdateAuction;
using Auktionshuset.Contracts.Dto.Admin.Auction;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Auktionshuset.Api.Endpoints.Admin.UpdateAuction;

public static class UpdateAuctionEndpoint
{
    /// <summary>
    /// Maps the update-auction endpoint onto the supplied route group.
    /// </summary>
    /// <param name="group">The route group that the endpoint is mapped onto.</param>
    /// <returns>The same route group so that further endpoints can be chained.</returns>
    public static RouteGroupBuilder MapUpdateAuction(this RouteGroupBuilder group)
    {
        group.MapPut("/{auctionId:guid}", HandleAsync)
            .WithName("UpdateAuction")
            .WithSummary("Updates an existing auction")
            .Produces<UpdateAuctionResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesValidationProblem();

        return group;
    }

    /// <summary>
    /// Replaces the auction identified by the route with the supplied values.
    /// </summary>
    /// <param name="auctionId">The identifier of the auction to update.</param>
    /// <param name="request">The replacement auction values.</param>
    /// <param name="handler">The handler that updates the auction.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The updated auction summary, a validation problem, or 404.</returns>
    public static async Task<Results<Ok<UpdateAuctionResponse>, NotFound, ValidationProblem>> HandleAsync(
        Guid auctionId,
        UpdateAuctionRequest request,
        UpdateAuctionHandler handler,
        CancellationToken cancellationToken)
    {
        var command = new UpdateAuctionCommand(
            AuctionId: auctionId,
            Name: request.Name.Trim(),
            StartsAt: request.StartsAt!.Value,
            EndsAt: request.EndsAt!.Value,
            EmployeeId: request.EmployeeId!.Value,
            AuctionHouseId: request.AuctionHouseId,
            Lots: AuctionEndpointMapping.ToSelections(request.Lots));

        var result = await handler.HandleAsync(command, cancellationToken);

        if (result.NotFound)
        {
            return TypedResults.NotFound();
        }

        if (!result.Succeeded)
        {
            return TypedResults.ValidationProblem(AuctionEndpointMapping.ToValidationErrors(result.Errors));
        }

        return TypedResults.Ok(new UpdateAuctionResponse(result.AuctionId, result.LotCount, result.ItemCount));
    }
}
