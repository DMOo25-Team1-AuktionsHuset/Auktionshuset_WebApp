using Auktionshuset.Application.Admin.Auctions;
using Auktionshuset.Application.Admin.Auctions.GetAuctions;
using Auktionshuset.Contracts.Dto.Admin.Auction;
using Auktionshuset.Contracts.Dto.Admin.Lot.Image;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Auktionshuset.Api.Endpoints.Admin.GetAuctions;

public static class GetAuctionsEndpoint
{
    /// <summary>
    /// Maps the auction dashboard and auction detail endpoints onto the supplied route group.
    /// </summary>
    /// <param name="group">The route group that the endpoints are mapped onto.</param>
    /// <returns>The same route group so that further endpoints can be chained.</returns>
    public static RouteGroupBuilder MapGetAuctions(this RouteGroupBuilder group)
    {
        group.MapGet("/", HandleGetAllAsync)
            .WithName("GetAuctions")
            .WithSummary("Gets every auction for the dashboard")
            .Produces<IReadOnlyList<AuctionListItemResponse>>();

        group.MapGet("/{auctionId:guid}", HandleGetByIdAsync)
            .WithName("GetAuction")
            .WithSummary("Gets a single auction with its lot lines")
            .Produces<AuctionDetailResponse>()
            .Produces(StatusCodes.Status404NotFound);

        return group;
    }

    /// <summary>
    /// Returns every auction projected into the dashboard row contract. The status is derived from
    /// the times so the list never shows a stale value.
    /// </summary>
    /// <param name="handler">The handler that supplies every auction.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A 200 response containing every auction.</returns>
    public static async Task<Ok<IReadOnlyList<AuctionListItemResponse>>> HandleGetAllAsync(
        GetAuctionsHandler handler,
        CancellationToken cancellationToken)
    {
        var auctions = await handler.HandleAsync(cancellationToken);
        var now = DateTime.Now;

        var response = auctions
            .Select(overview => new AuctionListItemResponse(
                AuctionId: overview.Auction.AuctionId,
                Name: overview.Auction.Name,
                Status: AuctionStatuses.Derive(overview.Auction.StartsAt, overview.Auction.EndedAt, now),
                StartsAt: overview.Auction.StartsAt,
                EndsAt: overview.Auction.EndedAt,
                EmployeeId: overview.Auction.EmployeeId,
                EmployeeName: overview.EmployeeName,
                LotCount: overview.LotCount,
                ItemCount: overview.ItemCount,
                ImageUrls: ToImageUrls(overview.ImageFileNames)))
            .ToArray();

        return TypedResults.Ok<IReadOnlyList<AuctionListItemResponse>>(response);
    }

    /// <summary>
    /// Returns a single auction with its lot lines, or 404 when it does not exist.
    /// </summary>
    /// <param name="auctionId">The identifier of the auction to load.</param>
    /// <param name="handler">The handler that supplies the auction.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A 200 response containing the auction, or 404.</returns>
    public static async Task<Results<Ok<AuctionDetailResponse>, NotFound>> HandleGetByIdAsync(
        Guid auctionId,
        GetAuctionsHandler handler,
        CancellationToken cancellationToken)
    {
        var detail = await handler.HandleByIdAsync(auctionId, cancellationToken);

        if (detail is null)
        {
            return TypedResults.NotFound();
        }

        var response = new AuctionDetailResponse(
            AuctionId: detail.Auction.AuctionId,
            Name: detail.Auction.Name,
            Status: AuctionStatuses.Derive(detail.Auction.StartsAt, detail.Auction.EndedAt, DateTime.Now),
            StartsAt: detail.Auction.StartsAt,
            EndsAt: detail.Auction.EndedAt,
            EmployeeId: detail.Auction.EmployeeId,
            EmployeeName: detail.EmployeeName,
            LotCount: detail.LotCount,
            ItemCount: detail.ItemCount,
            Lots: detail.Lots
                .Select(line => new AuctionLotResponse(
                    LotId: line.LotId,
                    Name: line.Name,
                    Category: line.Category,
                    Quantity: line.Quantity,
                    EstimatedValue: line.EstimatedValue,
                    ImageUrl: LotImagePaths.ToUrl(line.ImageFileName)))
                .ToArray());

        return TypedResults.Ok(response);
    }

    private static IReadOnlyList<string> ToImageUrls(IReadOnlyList<string> fileNames) =>
        fileNames
            .Select(LotImagePaths.ToUrl)
            .OfType<string>()
            .ToArray();
}
