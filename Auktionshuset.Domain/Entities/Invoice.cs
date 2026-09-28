using System;

namespace Auktionshuset.Domain.Entities
{
    public class Invoice
    {
        public required Guid InvoiceId { get; set; }
        public required int InvoiceNumber { get; set; }
        public required DateTime InvoiceDateTime { get; set; }
        public required string Status { get; set; }
        public required Guid AuctionLotId { get; set; }
        public AuctionLot AuctionLot { get; set; } = null!;
        public required Guid CustomerId { get; set; }
        public Customer Customer { get; set; } = null!;
        public required Guid WinningBidId { get; set; }
        public Bid WinningBid { get; set; } = null!;
    }
}
