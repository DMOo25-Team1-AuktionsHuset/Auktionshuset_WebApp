using Auktionshuset.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Auktionshuset.Infrastructure.Data.Configuration
{
    /// <summary>
    /// Configures the <see cref="Auction"/> entity, including its relationships to
    /// <see cref="AuctionHouse"/>, <see cref="Employee"/> and its lot lines.
    /// </summary>
    public class AuctionConfiguration : IEntityTypeConfiguration<Auction>
    {
        /// <summary>
        /// Applies the configuration for <see cref="Auction"/>.
        /// </summary>
        /// <param name="entity">The builder used to configure the <see cref="Auction"/> entity.</param>
        public void Configure(EntityTypeBuilder<Auction> entity)
        {
            entity.HasKey(a => a.AuctionId);
            entity.Property(a => a.Name).IsRequired().HasMaxLength(120);
            entity.Property(a => a.StartsAt).IsRequired();
            entity.Property(a => a.EndsAt).IsRequired();
            entity.Property(a => a.AuctionStatus).IsRequired().HasMaxLength(32);

            entity.HasOne(a => a.AuctionHouse)
                .WithMany()
                .HasForeignKey(a => a.AuctionHouseId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(a => a.Employee)
                .WithMany()
                .HasForeignKey(a => a.EmployeeId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
