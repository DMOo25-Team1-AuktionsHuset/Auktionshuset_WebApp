using Auktionshuset.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Auktionshuset.Infrastructure.Data.Configuration
{
    public class DeviceSessionConfiguration : IEntityTypeConfiguration<DeviceSession>
    {
        public void Configure(EntityTypeBuilder<DeviceSession> entity)
        {
            entity.HasKey(s => s.DeviceSessionId);

            entity.HasOne(s => s.Customer)
                .WithMany()
                .HasForeignKey(s => s.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(s => s.Auction)
                .WithMany()
                .HasForeignKey(s => s.AuctionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(s => s.Device)
                .WithMany()
                .HasForeignKey(s => s.DeviceId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
