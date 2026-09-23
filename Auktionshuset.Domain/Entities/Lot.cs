using System;
using System.Collections.Generic;

namespace Auktionshuset.Domain.Entities
{
    public class Lot
    {
        public required Guid LotId { get; set; }
        public required Guid AuctionHouseId { get; set; }
        public AuctionHouse AuctionHouse { get; set; } = null!;
        public required string Name { get; set; }
        public required string Category { get; set; }
        public required int Quantity { get; set; }
        public required decimal EstimatedValue { get; set; }
        public required string Description { get; set; }
        public List<string> Tags { get; set; } = new List<string>();

        /// <summary>
        /// The bare file name of the image attached to the lot, or <see langword="null"/> when the
        /// lot has no image.
        /// </summary>
        public string? ImageFileName { get; set; }

        public ICollection<AuctionLot> AuctionLots { get; set; } = new List<AuctionLot>();
    }
}
