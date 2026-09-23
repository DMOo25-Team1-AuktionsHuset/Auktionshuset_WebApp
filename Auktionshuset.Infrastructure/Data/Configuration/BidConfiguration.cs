using System;
using System.Collections.Generic;
using System.Text;
using Auktionshuset.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Auktionshuset.Infrastructure.Data.Configuration
{
    public class BidConfiguration : IEntityTypeConfiguration<Bid>
    {
        public void Configure(EntityTypeBuilder<Bid> entity)
        {
            entity.HasKey(b => b.BidId);
            entity.Property(b => b.Amount).IsRequired();
            entity.Property(b => b.PlacedAt).IsRequired();

            entity.HasOne(b => b.AuctionLot)
                .WithMany(l => l.Bids)
                .HasForeignKey(b => b.AuctionLotId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(b => b.DeviceSession)
                .WithMany(u => u.Bids)
                .HasForeignKey(b => b.DeviceSessionId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
