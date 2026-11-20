using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Auktionshuset.Infrastructure.Data.Configuration;

public sealed class DeviceBidRecoveryConfiguration : IEntityTypeConfiguration<DeviceBidRecovery>
{
    public void Configure(EntityTypeBuilder<DeviceBidRecovery> entity)
    {
        entity.HasKey(r => new { r.DeviceId, r.RequestId });
        entity.Property(r => r.Amount).HasPrecision(18, 2);
        entity.Property(r => r.ClearedBy).HasMaxLength(128);
        entity.HasIndex(r => r.SessionId);
    }
}
