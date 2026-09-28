using Auktionshuset.Domain.Entities;
using Auktionshuset.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Auktionshuset.Infrastructure.Test;

public class AHDBContextModelTests
{
    [Fact]
    public void Model_UsesExplicitForeignKeysForBidRelationships()
    {
        var options = new DbContextOptionsBuilder<AHDBContext>()
            .UseNpgsql("Host=localhost;Database=auction-model-test;Username=postgres;Password=postgres")
            .Options;
        using var context = new AHDBContext(options);

        var model = context.Model;
        var auctionLotType = model.FindEntityType(typeof(AuctionLot))!;
        var bidType = model.FindEntityType(typeof(Bid))!;

        var currentHighestBid = Assert.Single(auctionLotType.GetForeignKeys(), foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(Bid)
            && foreignKey.DependentToPrincipal?.Name == nameof(AuctionLot.CurrentHighestBid));
        var bidAuctionLot = Assert.Single(bidType.GetForeignKeys(), foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(AuctionLot)
            && foreignKey.DependentToPrincipal?.Name == nameof(Bid.AuctionLot));

        Assert.Equal(nameof(AuctionLot.CurrentHighestBidId), Assert.Single(currentHighestBid.Properties).Name);
        Assert.False(Assert.Single(currentHighestBid.Properties).IsShadowProperty());
        Assert.False(currentHighestBid.IsRequired);
        Assert.Equal(nameof(Bid.AuctionLotId), Assert.Single(bidAuctionLot.Properties).Name);
        Assert.False(Assert.Single(bidAuctionLot.Properties).IsShadowProperty());

        var shadowForeignKeys = model.GetEntityTypes()
            .SelectMany(entityType => entityType.GetForeignKeys())
            .Where(foreignKey => foreignKey.Properties.Any(property => property.IsShadowProperty()));
        Assert.Empty(shadowForeignKeys);

        Assert.Equal("jsonb", model.FindEntityType(typeof(Lot))!
            .FindProperty(nameof(Lot.Tags))!
            .GetColumnType());
    }
}
