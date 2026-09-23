using Auktionshuset.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Auktionshuset.Infrastructure.Data.Configuration;

public sealed class DeviceConfiguration : IEntityTypeConfiguration<Device>
{
    public void Configure(EntityTypeBuilder<Device> entity)
    {
        entity.HasKey(device => device.DeviceId);
        entity.Property(device => device.DeviceNumber).IsRequired();
        entity.Property(device => device.Status).IsRequired();

        entity.HasOne(device => device.AuctionHouse)
            .WithMany(auctionHouse => auctionHouse.Devices)
            .HasForeignKey(device => device.AuctionHouseId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
