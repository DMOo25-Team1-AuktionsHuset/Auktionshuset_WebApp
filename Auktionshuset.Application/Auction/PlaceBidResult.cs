namespace Auktionshuset.Application.Auction {
    public sealed record PlaceBidResult(
        bool Accepted, 
        bool Duplicate, 
        Guid? BidId, 
        int? SequenceNumber, 
        decimal CurrentPrice, 
        string? ErrorCode);
}
