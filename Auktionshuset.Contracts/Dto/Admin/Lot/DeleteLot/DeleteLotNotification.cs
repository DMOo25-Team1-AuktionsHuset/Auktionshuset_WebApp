namespace Auktionshuset.Contracts.Dto.Admin.Lot.DeleteLot
{
    public sealed record DeleteLotNotification(Guid EventId, Guid LotId, DateTime OccurredAt);
}
