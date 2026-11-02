namespace Auktionshuset.Application.Abstraction.Auth;

public enum DeviceAssignmentStatus
{
    Success,
    InvalidRequest,
    DeviceNotFound,
    DeviceDisabled,
    AuctionNotFound,
    CustomerNotFound,
    AuctionHouseMismatch,
    DeviceAlreadyAssigned,
    CustomerAlreadyAssigned,
    SessionNotFound
}

public sealed record DeviceAssignmentResult(DeviceAssignmentStatus Status, Guid? SessionId = null);

public interface IDeviceAssignmentRepository
{
    Task<DeviceAssignmentResult> IssueAsync(Guid deviceId, Guid auctionId, Guid customerId, CancellationToken cancellationToken);
    Task<DeviceAssignmentResult> ReleaseAsync(Guid deviceId, Guid auctionId, Guid sessionId, CancellationToken cancellationToken);
    Task<DeviceAssignmentResult> DeactivateAsync(Guid deviceId, CancellationToken cancellationToken);
}
