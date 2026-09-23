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
            entity.Property(b => b.Amount).HasPrecision(18, 2);
            entity.Property(b => b.PlacedAt).IsRequired();

            entity.HasOne(b => b.AuctionLot)
                .WithMany()
                .HasForeignKey(b => b.AuctionLotId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(b => b.DeviceSession)
                .WithMany()
                .HasForeignKey(b => b.DeviceSessionId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(b => b.Customer)
                .WithMany()
                .HasForeignKey(b => b.CustomerId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(b => new { b.AuctionLotId, b.SequenceNumber })
                .IsUnique();

            // supports finding the highest bid and tie-breaking by sequence
            entity.HasIndex(b => new
            {
                b.AuctionLotId,
                b.Amount,
                b.SequenceNumber
            }).IsDescending(false, true, false);
        }
    }
}
