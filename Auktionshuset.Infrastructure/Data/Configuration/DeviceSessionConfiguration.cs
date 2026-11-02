using Auktionshuset.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Auktionshuset.Infrastructure.Data.Configuration
{
    public class DeviceSessionConfiguration : IEntityTypeConfiguration<DeviceSession>
    {
        public void Configure(EntityTypeBuilder<DeviceSession> entity)
        {
            entity.HasKey(session => session.DeviceSessionId);
            entity.Property(session => session.StartedAt).IsRequired();
            entity.Property(session => session.EndedAt);

            entity.HasIndex(session => session.CustomerId);
            entity.HasIndex(session => session.DeviceId);
            entity.HasIndex(session => session.DeviceId, "IX_DeviceSessions_Active_DeviceId")
                .IsUnique()
                .HasFilter("\"EndedAt\" IS NULL");
            entity.HasIndex(session => new { session.CustomerId, session.AuctionId }, "IX_DeviceSessions_Active_CustomerId_AuctionId")
                .IsUnique()
                .HasFilter("\"EndedAt\" IS NULL");

            entity.HasOne(session => session.Customer)
                .WithMany(customer => customer.DeviceSessions)
                .HasForeignKey(session => session.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(session => session.Auction)
                .WithMany(auction => auction.DeviceSessions)
                .HasForeignKey(session => session.AuctionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(session => session.Device)
                .WithMany(device => device.DeviceSessions)
                .HasForeignKey(session => session.DeviceId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
