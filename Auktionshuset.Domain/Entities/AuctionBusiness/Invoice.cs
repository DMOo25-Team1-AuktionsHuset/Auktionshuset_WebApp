using System;
using System.Collections.Generic;
using System.Text;
using System.ComponentModel.DataAnnotations;
using Auktionshuset.Domain.Entities.Bid;

namespace Auktionshuset.Domain.Entities.AuctionBusiness
{
    public class Invoice
    {
        public required Guid InvoiceId { get; set; }
        public required int InvoiceName { get; set; }
        public required DateTime InvoiceDateTime { get; set; }

        public required AuctionLot AuctionLot { get; set; }
        public required Guid AuctionLotId { get; set; }
        public required AuctionBusiness.Customer Customer { get; set; }
        public required Guid CustomerId { get; set; }
        //Det skal vi lige finde ud af - beregne??
        public required Bid.Bid WinningBid { get; set; }

    }
}
