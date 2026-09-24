using Xunit;
using Auktionshuset.Application.Admin.Auctions;
using Auktionshuset.Domain.Entities;
using Auktionshuset.Infrastructure.Service;

namespace Auktionshuset.Tests;

public class InMemoryAuctionRepositoryTests
{
    /// <summary>
    /// Verifies that every stored auction is returned with the newest start time first.
    /// </summary>
    [Fact]
    public async Task GetAllAsync_WithSeveralAuctions_OrdersByStartTimeDescending()
    {
        var repository = new InMemoryAuctionRepository();
        Auction earliest = CreateAuction("Tidlig", DateTime.Now.AddDays(1));
        Auction latest = CreateAuction("Sen", DateTime.Now.AddDays(10));

        await repository.AddAsync(earliest, [], CancellationToken.None);
        await repository.AddAsync(latest, [], CancellationToken.None);

        IReadOnlyList<Auction> auctions = await repository.GetAllAsync(CancellationToken.None);

        Assert.Equal([latest.AuctionId, earliest.AuctionId], auctions.Select(auction => auction.AuctionId));
    }

    /// <summary>
    /// Verifies that only the requested auction and its lot lines are removed.
    /// </summary>
    [Fact]
    public async Task DeleteAsync_RemovesAuctionAndItsLotLines()
    {
        var repository = new InMemoryAuctionRepository();
        Lot lot = TestData.CreateLot("Stol");
        Auction auction = CreateAuction("Forårsauktion", DateTime.Now.AddDays(2));
        Auction other = CreateAuction("Efterårsauktion", DateTime.Now.AddDays(20));

        await repository.AddAsync(auction, [CreateAuctionLot(auction, lot, 2)], CancellationToken.None);
        await repository.AddAsync(other, [], CancellationToken.None);

        bool deleted = await repository.DeleteAsync(auction.AuctionId, CancellationToken.None);

        Assert.True(deleted);
        Assert.Null(await repository.GetByIdAsync(auction.AuctionId, CancellationToken.None));
        Assert.Empty(await repository.GetAuctionLotsAsync(auction.AuctionId, CancellationToken.None));
        Assert.NotNull(await repository.GetByIdAsync(other.AuctionId, CancellationToken.None));
    }

    /// <summary>
    /// Verifies that deleting an unknown auction reports false.
    /// </summary>
    [Fact]
    public async Task DeleteAsync_WithUnknownAuction_ReturnsFalse()
    {
        bool deleted = await new InMemoryAuctionRepository().DeleteAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.False(deleted);
    }

    /// <summary>
    /// Verifies that updating replaces the stored auction and its lot lines.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_WithExistingAuction_ReplacesValuesAndLotLines()
    {
        var repository = new InMemoryAuctionRepository();
        Lot first = TestData.CreateLot("Stol", quantity: 4);
        Lot second = TestData.CreateLot("Bord", quantity: 6);
        Auction auction = CreateAuction("Forårsauktion", DateTime.Now.AddDays(2));

        await repository.AddAsync(auction, [CreateAuctionLot(auction, first, 1)], CancellationToken.None);

        auction.Name = "Efterårsauktion";
        auction.StartsAt = DateTime.Now.AddDays(30);

        bool updated = await repository.UpdateAsync(
            auction,
            [CreateAuctionLot(auction, second, 3)],
            CancellationToken.None);

        Assert.True(updated);

        Auction? stored = await repository.GetByIdAsync(auction.AuctionId, CancellationToken.None);
        Assert.Equal("Efterårsauktion", stored!.Name);

        AuctionLot line = Assert.Single(await repository.GetAuctionLotsAsync(auction.AuctionId, CancellationToken.None));
        Assert.Equal(second.LotId, line.LotId);
        Assert.Equal(3, line.Quantity);
    }

    /// <summary>
    /// Verifies that updating an unknown auction reports false.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_WithUnknownAuction_ReturnsFalse()
    {
        Auction auction = CreateAuction("Ukendt", DateTime.Now.AddDays(2));

        bool updated = await new InMemoryAuctionRepository().UpdateAsync(auction, [], CancellationToken.None);

        Assert.False(updated);
    }

    /// <summary>
    /// Verifies that an unknown auction has no lot lines.
    /// </summary>
    [Fact]
    public async Task GetAuctionLotsAsync_WithUnknownAuction_ReturnsEmpty()
    {
        IReadOnlyList<AuctionLot> lines = await new InMemoryAuctionRepository().GetAuctionLotsAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Empty(lines);
    }

    /// <summary>
    /// Verifies that adding the same auction twice is refused.
    /// </summary>
    [Fact]
    public async Task AddAsync_WithDuplicateId_Throws()
    {
        var repository = new InMemoryAuctionRepository();
        Auction auction = CreateAuction("Forårsauktion", DateTime.Now.AddDays(2));

        await repository.AddAsync(auction, [], CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            repository.AddAsync(auction, [], CancellationToken.None));
    }

    /// <summary>
    /// Verifies that a cancelled request is honoured.
    /// </summary>
    [Fact]
    public async Task GetAllAsync_WhenCancelled_Throws()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new InMemoryAuctionRepository().GetAllAsync(cancellation.Token));
    }

    private static Auction CreateAuction(string name, DateTime startsAt) => new()
    {
        AuctionId = Guid.NewGuid(),
        Name = name,
        StartsAt = startsAt,
        EndsAt = startsAt.AddHours(3),
        EmployeeId = Guid.NewGuid(),
        AuctionHouseId = TestData.AuctionHouseId,
        AuctionStatus = AuctionStatuses.Upcoming
    };

    private static AuctionLot CreateAuctionLot(Auction auction, Lot lot, int quantity) => new()
    {
        AuctionLotId = Guid.NewGuid(),
        AuctionId = auction.AuctionId,
        Auction = auction,
        LotId = lot.LotId,
        Lot = lot,
        Quantity = quantity
    };
}
