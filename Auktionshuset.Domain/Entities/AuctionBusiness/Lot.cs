using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Auktionshuset.Domain.Entities.AuctionBusiness
{
    public class Lot
    {
        public required Guid LotId { get; set; }
        public required string LotName { get; set; }
        public required string Category { get; set; }
        public required int Quantity { get; set; }
        public required decimal Value { get; set; }
        public required string Description { get; set; }
    }
}
