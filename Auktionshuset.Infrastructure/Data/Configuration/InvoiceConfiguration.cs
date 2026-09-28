using Auktionshuset.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Auktionshuset.Infrastructure.Data.Configuration;

public sealed class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> entity)
    {
        entity.HasKey(invoice => invoice.InvoiceId);
        entity.Property(invoice => invoice.InvoiceNumber).IsRequired();
        entity.Property(invoice => invoice.InvoiceDateTime).IsRequired();
        entity.Property(invoice => invoice.Status).IsRequired();

        entity.HasOne(invoice => invoice.AuctionLot)
            .WithMany(auctionLot => auctionLot.Invoices)
            .HasForeignKey(invoice => invoice.AuctionLotId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(invoice => invoice.Customer)
            .WithMany(customer => customer.Invoices)
            .HasForeignKey(invoice => invoice.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(invoice => invoice.WinningBid)
            .WithMany(bid => bid.WinningInvoices)
            .HasForeignKey(invoice => invoice.WinningBidId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
