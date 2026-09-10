using System;
using System.Collections.Generic;
using System.Text;

namespace Auktionshuset.Domain.Entities {
    public class Lot {
        public required Guid LotId { get; set; }
        public required string Name { get; set; }
        public required string Category { get; set; }
        public required int Quatity { get; set; }
        public required decimal EstimatedValue { get; set; }
        public required string Description { get; set; }
        public required List<string> Tags { get; set; } = new List<string>();

        public required AuctionHouse AuctionHouseId { get; set; }
    }
}
