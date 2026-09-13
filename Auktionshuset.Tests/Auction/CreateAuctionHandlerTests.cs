using Auktionshuset.Application.Abstraction.Admin.Auctions;
using Auktionshuset.Domain.Entities;
using Auktionshuset.Infrastructure.Service;
using Xunit;

namespace Auktionshuset.Tests;

public class CreateAuctionHandlerTests
{
    private static Lot CreateLot(string name) => new()
    {
        LotId = Guid.NewGuid(),
        Name = name,
        Category = "Møbler",
        Quantity = 1,
        EstimatedValue = 500m,
        Description = "En genstand",
        Tags = ["træ"],
        AuctionHouseId = Guid.NewGuid()
    };

    private static async Task<(CreateAuctionHandler Handler, InMemoryAuctionRepository Auctions)> CreateHandlerAsync(
        params Lot[] lots)
    {
        var lotRepository = new InMemoryLotRepository();
        foreach (var lot in lots)
        {
            await lotRepository.AddAsync(lot, CancellationToken.None);
        }

        var auctionRepository = new InMemoryAuctionRepository();

        return (new CreateAuctionHandler(auctionRepository, lotRepository), auctionRepository);
    }

    [Fact]
    public async Task HandleAsync_WithSelectedLots_CreatesAuctionWithRelationships()
    {
        var first = CreateLot("Stol");
        var second = CreateLot("Bord");
        var (handler, auctions) = await CreateHandlerAsync(first, second);

        var result = await handler.HandleAsync(
            new CreateAuctionCommand(DateTime.Now.AddDays(7), [first.LotId, second.LotId]),
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.NotEqual(Guid.Empty, result.AuctionId);
        Assert.Equal(2, result.LotCount);

        var auction = await auctions.GetByIdAsync(result.AuctionId, CancellationToken.None);
        Assert.NotNull(auction);

        var auctionLots = await auctions.GetAuctionLotsAsync(result.AuctionId, CancellationToken.None);
        var linkedLotIds = auctionLots.Select(link => link.LotId.LotId).ToHashSet();
        Assert.Equal(2, linkedLotIds.Count);
        Assert.Contains(first.LotId, linkedLotIds);
        Assert.Contains(second.LotId, linkedLotIds);
    }

    [Fact]
    public async Task HandleAsync_WithoutLots_CreatesAuctionWithNoRelationships()
    {
        var (handler, auctions) = await CreateHandlerAsync();

        var result = await handler.HandleAsync(
            new CreateAuctionCommand(DateTime.Now.AddDays(1), []),
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.NotEqual(Guid.Empty, result.AuctionId);
        Assert.Equal(0, result.LotCount);

        var auction = await auctions.GetByIdAsync(result.AuctionId, CancellationToken.None);
        Assert.NotNull(auction);
        Assert.Empty(await auctions.GetAuctionLotsAsync(result.AuctionId, CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_WithPastDate_ReturnsInvalid()
    {
        var (handler, auctions) = await CreateHandlerAsync();

        var result = await handler.HandleAsync(
            new CreateAuctionCommand(DateTime.Now.AddDays(-1), []),
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Contains("fremtiden"));
        Assert.Equal(Guid.Empty, result.AuctionId);
        Assert.Empty(await auctions.GetAuctionLotsAsync(result.AuctionId, CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_WithDuplicateLotIds_ReturnsInvalid()
    {
        var lot = CreateLot("Lampe");
        var (handler, _) = await CreateHandlerAsync(lot);

        var result = await handler.HandleAsync(
            new CreateAuctionCommand(DateTime.Now.AddDays(2), [lot.LotId, lot.LotId]),
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Contains("mere end én gang"));
    }

    [Fact]
    public async Task HandleAsync_WithUnknownLot_ReturnsInvalid()
    {
        var (handler, _) = await CreateHandlerAsync();

        var result = await handler.HandleAsync(
            new CreateAuctionCommand(DateTime.Now.AddDays(2), [Guid.NewGuid()]),
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Contains("findes ikke"));
    }
}
