using System.Text.Json;
using Auktionshuset.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Auktionshuset.Infrastructure.Data.Configuration {
    public class LotConfiguration : IEntityTypeConfiguration<Lot>
    {
        /// <summary>
        /// Configures the <see cref="Lot"/> entity, including its required properties and its
        /// relationship to <see cref="AuctionHouse"/>.
        /// </summary>
        /// <param name="entity">The builder used to configure the <see cref="Lot"/> entity.</param>
        public void Configure(EntityTypeBuilder<Lot> entity)
        {
            entity.HasKey(l => l.LotId);
            entity.Property(l => l.Name).IsRequired();
            entity.Property(l => l.Category).IsRequired();
            entity.Property(l => l.Quantity).IsRequired();
            entity.Property(l => l.EstimatedValue).IsRequired();
            entity.Property(l => l.Description).IsRequired();
            var tags = entity.Property(l => l.Tags)
                .IsRequired()
                .HasColumnType("jsonb")
                .HasConversion(
                    value => JsonSerializer.Serialize(value, (JsonSerializerOptions?)null),
                    value => JsonSerializer.Deserialize<List<string>>(value, (JsonSerializerOptions?)null)
                        ?? new List<string>());
            tags.Metadata.SetValueComparer(new ValueComparer<List<string>>(
                (left, right) => left == null
                    ? right == null
                    : right != null && left.SequenceEqual(right),
                value => value == null
                    ? 0
                    : value.Aggregate(0, (hash, tag) => HashCode.Combine(hash, tag.GetHashCode())),
                value => value == null ? new List<string>() : value.ToList()));
            entity.Property(l => l.ImageFileName).HasMaxLength(128);

            entity.HasOne(l => l.AuctionHouse)
                .WithMany(ah => ah.Lots)
                .HasForeignKey(l => l.AuctionHouseId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
