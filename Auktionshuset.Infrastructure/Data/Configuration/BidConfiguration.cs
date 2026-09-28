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
        entity.HasKey(bid => bid.BidId);
        entity.Property(bid => bid.Amount).IsRequired();
        entity.Property(bid => bid.PlacedAt).IsRequired();
        entity.Property(bid => bid.SequenceNumber).IsRequired();

        entity.HasOne(bid => bid.AuctionLot)
            .WithMany(auctionLot => auctionLot.Bids)
            .HasForeignKey(bid => bid.AuctionLotId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(bid => bid.DeviceSession)
            .WithMany(session => session.Bids)
            .HasForeignKey(bid => bid.DeviceSessionId)
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
