namespace Auktionshuset.Domain.Entities;

public sealed class AuctionRegistration
{
    public Guid AuctionId { get; set; }
    public Auction Auction { get; set; } = null!;
    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public DateTime RegisteredAt { get; set; }
}
