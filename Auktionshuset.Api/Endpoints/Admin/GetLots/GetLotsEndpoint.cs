using Auktionshuset.Application.Admin.Lots;
using Auktionshuset.Api.Security;
using Auktionshuset.Contracts.Dto.Admin.Lot;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Auktionshuset.Api.Endpoints.Admin.GetLots;

public static class GetLotsEndpoint
{
    /// <summary>
    /// Maps the get-lots endpoint onto the supplied route group.
    /// </summary>
    /// <param name="group">The route group that the endpoint is mapped onto.</param>
    /// <returns>The same route group so that further endpoints can be chained.</returns>
    public static RouteGroupBuilder MapGetLots(this RouteGroupBuilder group)
    {
        group.MapGet("/", HandleAsync)
            .WithName("GetLots")
            .WithSummary("Gets all auction lots")
            .Produces<IReadOnlyList<LotListItemResponse>>()
            .RequireAuthorization(SecurityPolicies.CanViewLots);

        return group;
    }

    /// <summary>
    /// Returns all lots projected into the list item contract.
    /// </summary>
    /// <param name="handler">The handler that supplies all lots.</param>
    /// <returns>A 200 response containing every lot projected into <see cref="LotListItemResponse"/>.</returns>
    private static async Task<Ok<IReadOnlyList<LotListItemResponse>>> HandleAsync(
        GetLotsHandler handler,
        CancellationToken cancellationToken)
    {
        var lots = await handler.HandleAsync(cancellationToken);
        var response = lots
            .Select(lot => new LotListItemResponse(
                lot.LotId,
                lot.Name,
                lot.Category,
                lot.Quantity,
                lot.EstimatedValue,
                lot.Description,
                lot.Tags,
                lot.AuctionHouseId))
            .ToArray();

        return TypedResults.Ok<IReadOnlyList<LotListItemResponse>>(response);
    }
}
