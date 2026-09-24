namespace Auktionshuset.Domain.Entities
{
    public class Lot
    {
        public required Guid LotId { get; set; }
        public required string Name { get; set; }
        public required string Category { get; set; }
        public required int Quantity { get; set; }
        public required decimal EstimatedValue { get; set; }
        public required string Description { get; set; }
        public required List<string> Tags { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets the file name of the image attached to the lot, or <see langword="null"/>
        /// when the lot has no image. Only the bare file name is stored, never a path.
        /// </summary>
        public string? ImageFileName { get; set; }

        public required Guid AuctionHouseId { get; set; }
        public AuctionHouse? AuctionHouse { get; set; }

        public ICollection<AuctionLot> AuctionLots { get; set; } = new List<AuctionLot>();
    }
}
