using Auktionshuset.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Auktionshuset.Infrastructure.Data.Configuration;

public sealed class AuctionRegistrationConfiguration : IEntityTypeConfiguration<AuctionRegistration>
{
    public void Configure(EntityTypeBuilder<AuctionRegistration> entity)
    {
        entity.HasKey(x => new { x.AuctionId, x.CustomerId });
        entity.HasOne(x => x.Auction).WithMany().HasForeignKey(x => x.AuctionId);
        entity.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId);
    }
}
