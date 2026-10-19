using Auktionshuset.Domain.Entities;
using Auktionshuset.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Auktionshuset.Infrastructure.Test;

public class AHDBContextModelTests
{
    [Fact]
    public void Model_UsesExplicitForeignKeysForBidRelationships()
    {
        DbContextOptions<AHDBContext> options = new DbContextOptionsBuilder<AHDBContext>()
            .UseNpgsql("Host=localhost;Database=auction-model-test;Username=postgres;Password=postgres")
            .Options;
        using var context = new AHDBContext(options);

        IModel model = context.Model;
        IEntityType auctionLotType = model.FindEntityType(typeof(AuctionLot))!;
        IEntityType bidType = model.FindEntityType(typeof(Bid))!;

        IForeignKey currentHighestBid = Assert.Single(auctionLotType.GetForeignKeys(), foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(Bid)
            && foreignKey.DependentToPrincipal?.Name == nameof(AuctionLot.CurrentHighestBid));
        IForeignKey bidAuctionLot = Assert.Single(bidType.GetForeignKeys(), foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(AuctionLot)
            && foreignKey.DependentToPrincipal?.Name == nameof(Bid.AuctionLot));

        Assert.Equal(nameof(AuctionLot.CurrentHighestBidId), Assert.Single(currentHighestBid.Properties).Name);
        Assert.False(Assert.Single(currentHighestBid.Properties).IsShadowProperty());
        Assert.False(currentHighestBid.IsRequired);
        Assert.Equal(nameof(Bid.AuctionLotId), Assert.Single(bidAuctionLot.Properties).Name);
        Assert.False(Assert.Single(bidAuctionLot.Properties).IsShadowProperty());

        IEnumerable<IForeignKey> shadowForeignKeys = model.GetEntityTypes()
            .SelectMany(entityType => entityType.GetForeignKeys())
            .Where(foreignKey => foreignKey.Properties.Any(property => property.IsShadowProperty()));
        Assert.Empty(shadowForeignKeys);

        Assert.Equal("jsonb", model.FindEntityType(typeof(Lot))!
            .FindProperty(nameof(Lot.Tags))!
            .GetColumnType());
    }
}
