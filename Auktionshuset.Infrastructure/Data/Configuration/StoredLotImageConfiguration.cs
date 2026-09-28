using Auktionshuset.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Auktionshuset.Infrastructure.Data.Configuration;

public sealed class StoredLotImageConfiguration : IEntityTypeConfiguration<StoredLotImage>
{
    public void Configure(EntityTypeBuilder<StoredLotImage> entity)
    {
        entity.HasKey(image => image.FileName);
        entity.Property(image => image.FileName).HasMaxLength(128);
        entity.Property(image => image.Content).HasColumnType("bytea").IsRequired();
        entity.Property(image => image.ContentType).HasMaxLength(32).IsRequired();
    }
}
