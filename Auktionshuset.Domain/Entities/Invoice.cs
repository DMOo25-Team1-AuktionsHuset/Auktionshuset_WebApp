using System;
using System.Collections.Generic;
using System.Text;

namespace Auktionshuset.Domain.Entities {
    public class Invoice {
        public required Guid InvoiceId { get; set; }
        public required int InvoiceNumber { get; set; }
        public required DateTime InvoiceDateTime { get; set; }
        public required string Status { get; set; }

        public required AuctionLot AuctionLotId { get; set; }
        public required Customer CustomerId { get; set; }
    }
}
