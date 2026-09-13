using Auktionshuset.Application.Abstraction.Admin.Lots;
using Auktionshuset.Contracts.Dto.Admin.Lot;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Auktionshuset.Api.Endpoints.Admin.GetLots;

public static class GetLotsEndpoint
{
    public static RouteGroupBuilder MapGetLots(this RouteGroupBuilder group)
    {
        group.MapGet("/", HandleAsync)
            .WithName("GetLots")
            .WithSummary("Gets all auction lots")
            .Produces<IReadOnlyList<LotListItemResponse>>();

        return group;
    }

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
