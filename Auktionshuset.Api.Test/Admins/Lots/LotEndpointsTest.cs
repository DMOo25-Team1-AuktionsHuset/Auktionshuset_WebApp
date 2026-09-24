using Auktionshuset.Api.Endpoints.Admin.Lot.GetLots;
using Auktionshuset.Api.Endpoints.Admin.Lot.UpdateLot;
using Auktionshuset.Application.Admin.Lots;
using Auktionshuset.Application.Admin.Lots.UpdateLot;
using Auktionshuset.Contracts.Dto.Admin.Lot;
using Auktionshuset.Contracts.Dto.Admin.Lot.UpdateLot;
using Auktionshuset.Domain.Entities;
using Auktionshuset.Infrastructure.Service;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Auktionshuset.Api.Test.Admins.Lots;

public class LotEndpointsTest
{
    [Fact]
    public async Task GetLots_ReturnsProjectedLotsAndImageUrls()
    {
        var repository = new InMemoryLotRepository();
        Lot withImage = CreateLot("With image", "lot.png");
        Lot withoutImage = CreateLot("Without image");
        await repository.AddAsync(withImage, CancellationToken.None);
        await repository.AddAsync(withoutImage, CancellationToken.None);

        Ok<IReadOnlyList<LotListItemResponse>> result = await GetLotsEndpoint.HandleAsync(
            new GetLotsHandler(repository),
            CancellationToken.None);

        IReadOnlyList<LotListItemResponse> lots = Assert.IsType<Ok<IReadOnlyList<LotListItemResponse>>>(result).Value!;
        Assert.Equal(2, lots.Count);

        LotListItemResponse projected = Assert.Single(lots, lot => lot.LotId == withImage.LotId);
        Assert.Equal(withImage.Name, projected.Name);
        Assert.Equal(withImage.Category, projected.Category);
        Assert.Equal(withImage.Quantity, projected.Quantity);
        Assert.Equal(withImage.EstimatedValue, projected.EstimatedValue);
        Assert.Equal(withImage.Description, projected.Description);
        Assert.Equal(withImage.Tags, projected.Tags);
        Assert.Equal(withImage.AuctionHouseId, projected.AuctionHouseId);
        Assert.Equal("/uploads/lots/lot.png", projected.ImageUrl);

        Assert.Null(Assert.Single(lots, lot => lot.LotId == withoutImage.LotId).ImageUrl);
    }

    [Fact]
    public async Task GetLots_WithoutLots_ReturnsEmptyList()
    {
        Ok<IReadOnlyList<LotListItemResponse>> result = await GetLotsEndpoint.HandleAsync(
            new GetLotsHandler(new InMemoryLotRepository()),
            CancellationToken.None);

        Assert.Empty(Assert.IsType<Ok<IReadOnlyList<LotListItemResponse>>>(result).Value!);
    }

    [Fact]
    public async Task Update_WithExistingLot_TrimsValuesAndRemovesDuplicateTags()
    {
        Lot lot = CreateLot("Original");
        var repository = new InMemoryLotRepository();
        await repository.AddAsync(lot, CancellationToken.None);
        var publisher = new RecordingEventPublisher();
        var handler = new UpdateLotHandler(repository, publisher);
        var request = new UpdateLotRequest
        {
            Name = "  Updated lot  ",
            Category = "  Furniture ",
            Quantity = 4,
            EstimatedValue = 725m,
            Description = "  Updated description  ",
            Tags = ["  wood ", "Furniture", "WOOD"],
            AuctionHouseId = lot.AuctionHouseId
        };

        Results<Ok<UpdateLotResponse>, NotFound> result = await UpdateLotEndpoint.HandleAsync(
            lot.LotId,
            request,
            handler,
            CancellationToken.None);

        UpdateLotResponse response = Assert.IsType<Ok<UpdateLotResponse>>(result.Result).Value!;
        Assert.Equal(lot.LotId, response.LotId);

        Lot? updated = await repository.GetByIdAsync(lot.LotId, CancellationToken.None);
        Assert.NotNull(updated);
        Assert.Equal("Updated lot", updated.Name);
        Assert.Equal("Furniture", updated.Category);
        Assert.Equal(4, updated.Quantity);
        Assert.Equal(725m, updated.EstimatedValue);
        Assert.Equal("Updated description", updated.Description);
        Assert.Equal(["wood", "Furniture"], updated.Tags);
        Assert.Single(publisher.OfType<LotUpdatedIntegrationEvent>());
    }

    [Fact]
    public async Task Update_WithUnknownLot_ReturnsNotFound()
    {
        Results<Ok<UpdateLotResponse>, NotFound> result = await UpdateLotEndpoint.HandleAsync(
            Guid.NewGuid(),
            new UpdateLotRequest
            {
                Name = "Updated lot",
                Category = "Furniture",
                Quantity = 1,
                EstimatedValue = 100m,
                Description = "Updated description",
                Tags = [],
                AuctionHouseId = Guid.NewGuid()
            },
            new UpdateLotHandler(new InMemoryLotRepository(), new RecordingEventPublisher()),
            CancellationToken.None);

        Assert.IsType<NotFound>(result.Result);
    }

    private static Lot CreateLot(string name, string? imageFileName = null) => new()
    {
        LotId = Guid.NewGuid(),
        Name = name,
        Category = "Furniture",
        Quantity = 2,
        EstimatedValue = 500m,
        Description = "A stored lot",
        Tags = ["wood", "vintage"],
        ImageFileName = imageFileName,
        AuctionHouseId = Guid.NewGuid()
    };
}
