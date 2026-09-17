using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using System;
using System.Collections.Generic;
using System.Text;
using Auktionshuset.Domain.Entities;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Auktionshuset.Infrastructure.Data.Configuration {
    public class LotConfiguration : IEntityTypeConfiguration<Lot>
    {
        public void Configure(EntityTypeBuilder<Lot> entity)
        {
            entity.HasKey(l => l.LotId);
            entity.Property(l => l.Name).IsRequired();
            entity.Property(l => l.Category).IsRequired();
            entity.Property(l => l.Quantity).IsRequired();
            entity.Property(l => l.EstimatedValue).IsRequired();
            entity.Property(l => l.Description).IsRequired();
            entity.Property(l => l.Tags).IsRequired();

            entity.HasOne(l => l.AuctionHouse)
                .WithMany(ah => ah.Lots)
                .HasForeignKey(l => l.AuctionHouseId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
