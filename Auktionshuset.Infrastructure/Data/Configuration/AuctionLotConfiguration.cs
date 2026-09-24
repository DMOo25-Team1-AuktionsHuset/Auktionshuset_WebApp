using Auktionshuset.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Auktionshuset.Infrastructure.Data.Configuration
{
    /// <summary>
    /// Configures the <see cref="AuctionLot"/> join entity, which carries the number of units an
    /// auction includes of a lot.
    /// </summary>
    public class AuctionLotConfiguration : IEntityTypeConfiguration<AuctionLot>
    {
        /// <summary>
        /// Applies the configuration for <see cref="AuctionLot"/>.
        /// </summary>
        /// <param name="entity">The builder used to configure the <see cref="AuctionLot"/> entity.</param>
        public void Configure(EntityTypeBuilder<AuctionLot> entity)
        {
            entity.HasKey(al => al.AuctionLotId);
            entity.Property(al => al.Quantity).IsRequired();

            entity.HasOne(al => al.Auction)
                .WithMany(a => a.AuctionLots)
                .HasForeignKey(al => al.AuctionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(al => al.Lot)
                .WithMany(l => l.AuctionLots)
                .HasForeignKey(al => al.LotId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(al => new { al.AuctionId, al.LotId }).IsUnique();
        }
    }
}
