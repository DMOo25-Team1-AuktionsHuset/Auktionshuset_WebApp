namespace Auktionshuset.Domain.Entities
{
    public class Auction
    {
        public required Guid AuctionId { get; set; }

        public required string Name { get; set; }

        public Guid? AuctionHouseId { get; set; }
        public AuctionHouse? AuctionHouse { get; set; }

        /// <summary>
        /// Gets or sets the identifier of the auctionarius. It is <see langword="null"/> when the
        /// auction has no employee assigned yet.
        /// </summary>
        public Guid? EmployeeId { get; set; }
        public Employee? Employee { get; set; }

        public required DateTime StartsAt { get; set; }
        public required DateTime EndsAt { get; set; }

        /// <summary>
        /// Gets or sets the auction status. The value is derived from the start and end time, but is
        /// stored as well so the persisted state reflects the status at the time it was written.
        /// </summary>
        public required string AuctionStatus { get; set; }

        public ICollection<AuctionLot> AuctionLots { get; set; } = new List<AuctionLot>();
    }
}
