namespace Auktionshuset.Infrastructure.Data;

public sealed class DeviceBidRecovery
{
    public Guid DeviceId { get; set; }
    public Guid RequestId { get; set; }
    public Guid SessionId { get; set; }
    public Guid CustomerId { get; set; }
    public Guid AuctionId { get; set; }
    public Guid AuctionLotId { get; set; }
    public long SessionRevision { get; set; }
    public long AuctionRevision { get; set; }
    public decimal Amount { get; set; }
    public DateTime ReportedAt { get; set; }
    public DateTime? ClearedAt { get; set; }
    public string? ClearedBy { get; set; }
}
