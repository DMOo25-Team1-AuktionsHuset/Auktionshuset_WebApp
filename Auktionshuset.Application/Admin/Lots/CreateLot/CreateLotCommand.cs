namespace Auktionshuset.Application.Admin.Lots.CreateLot
{
    public sealed record CreateLotCommand(
        string Name,
        string Category,
        int Quantity,
        decimal EstimatedValue,
        string Description,
        IReadOnlyCollection<string> Tags,
        Guid AuctionHouseId);
}
