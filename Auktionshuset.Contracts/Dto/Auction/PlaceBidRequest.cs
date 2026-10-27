namespace Auktionshuset.Contracts.Dto.Auction
{
    public sealed record PlaceBidRequest(Guid RequestId, decimal Amount);
}
