using Auktionshuset.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Text;

namespace Auktionshuset.Infrastructure.Data.Configuration
{
    public class BidCommandKeyConfiguration : IEntityTypeConfiguration<BidCommandKey>
    {
        public void Configure(EntityTypeBuilder<BidCommandKey> entity)
        {
            entity.HasKey(r => r.BidCommandKeyId);

            entity.Property(r => r.RequestedAmount).HasPrecision(18, 2);
            entity.Property(r => r.CurrentPrice).HasPrecision(18, 2);
            entity.Property(r => r.ErrorCode).HasMaxLength(64);

            entity.HasOne<Customer>()
                .WithMany()
                .HasForeignKey(key => key.CustomerId)
                .IsRequired()
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(r => new { r.CustomerId, r.RequestId }).IsUnique();
        }
    }
}
