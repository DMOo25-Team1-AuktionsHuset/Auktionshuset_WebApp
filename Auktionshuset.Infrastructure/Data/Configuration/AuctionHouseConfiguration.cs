using System;
using System.Collections.Generic;
using System.Text;
using Auktionshuset.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Auktionshuset.Infrastructure.Data.Configuration 
{
    public class AuctionHouseConfiguration : IEntityTypeConfiguration<AuctionHouse> 
    {
        /// <summary>
        /// Configures the <see cref="AuctionHouse"/> entity, its composite key and its required
        /// properties.
        /// </summary>
        /// <param name="entity">The builder used to configure the <see cref="AuctionHouse"/> entity.</param>
        public void Configure(EntityTypeBuilder<AuctionHouse> entity) 
        {
            entity.HasKey(ah => ah.AuctionHouseId);
            entity.Property(ah => ah.AuctionHouseName).IsRequired();
            entity.Property(ah => ah.Address).IsRequired();
            entity.Property(ah => ah.CVRNumber).IsRequired();
            entity.Property(ah => ah.PhoneNumber).IsRequired();
            entity.Property(ah => ah.Email).IsRequired();
        }
    }
}
