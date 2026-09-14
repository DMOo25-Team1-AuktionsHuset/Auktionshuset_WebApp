using System;
using System.Collections.Generic;
using System.Text;
using Auktionshuset.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Auktionshuset.Infrastructure.Data.Configuration {
    public class AuctionHouseConfiguration : IEntityTypeConfiguration<AuctionHouse> {
        public void Configure(EntityTypeBuilder<AuctionHouse> entity) {
            entity.HasKey(ah => new { ah.AuctionHouseId });

            entity.Property(ah => ah.AuctionHouseName);
        }
    }
}
