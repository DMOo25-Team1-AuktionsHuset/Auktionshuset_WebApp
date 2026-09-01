using System;
using System.Collections.Generic;
using System.Text;
using System.ComponentModel.DataAnnotations;
using Auktionshuset.Domain.Entities.Auction;

namespace Auktionshuset.Domain.Entities.Billing
{
    public class Invoice
    {
        public required Guid InvoiceId { get; set; }
        public required int InvoiceName { get; set; }
        public required DateTime InvoiceDateTime { get; set; }
        public required Guid AuctionLotId { get; set; }
        public required Guid CustomerId { get; set; }
        public required Bid WinningBid { get; set; }

    }
}
