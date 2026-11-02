using Auktionshuset.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Auktionshuset.Infrastructure.Data.Configuration;

public sealed class AuctionInvoiceConfiguration : IEntityTypeConfiguration<AuctionInvoice>
{
    public void Configure(EntityTypeBuilder<AuctionInvoice> entity)
    {
        entity.HasKey(i => i.Id);
        entity.HasIndex(i => i.Number).IsUnique();
        entity.HasIndex(i => i.Sequence).IsUnique();
        entity.HasIndex(i => new { i.AuctionId, i.CustomerId }).IsUnique();
        entity.HasOne(i => i.Auction).WithMany().HasForeignKey(i => i.AuctionId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(i => i.Customer).WithMany().HasForeignKey(i => i.CustomerId).OnDelete(DeleteBehavior.Restrict);
        foreach (var property in typeof(AuctionInvoice).GetProperties().Where(p => p.PropertyType == typeof(decimal) || p.PropertyType == typeof(decimal?)))
            entity.Property(property.Name).HasPrecision(20, 2);
        entity.Property(i => i.PremiumPercent).HasPrecision(7, 4);
        entity.Property(i => i.VatPercent).HasPrecision(7, 4);
    }
}

public sealed class AuctionInvoiceLineConfiguration : IEntityTypeConfiguration<AuctionInvoiceLine>
{
    public void Configure(EntityTypeBuilder<AuctionInvoiceLine> entity)
    {
        entity.HasKey(l => l.Id);
        entity.HasIndex(l => l.AuctionLotId).IsUnique();
        entity.HasIndex(l => l.WinningBidId).IsUnique();
        entity.HasOne(l => l.Invoice).WithMany(i => i.Lines).HasForeignKey(l => l.InvoiceId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(l => l.AuctionLot).WithMany().HasForeignKey(l => l.AuctionLotId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne(l => l.WinningBid).WithMany().HasForeignKey(l => l.WinningBidId).OnDelete(DeleteBehavior.Restrict);
        foreach (var property in typeof(AuctionInvoiceLine).GetProperties().Where(p => p.PropertyType == typeof(decimal)))
            entity.Property(property.Name).HasPrecision(20, 2);
    }
}

public sealed class BillingSequenceConfiguration : IEntityTypeConfiguration<BillingSequence>
{
    public void Configure(EntityTypeBuilder<BillingSequence> entity)
    {
        entity.HasKey(s => s.Id);
        entity.Property(s => s.Id).ValueGeneratedNever();
    }
}
