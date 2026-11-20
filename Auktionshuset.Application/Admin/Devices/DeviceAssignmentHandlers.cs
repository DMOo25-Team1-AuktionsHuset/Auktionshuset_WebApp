using Auktionshuset.Application.Abstraction.Auth;

namespace Auktionshuset.Application.Admin.Devices;

public sealed record IssueDeviceCommand(Guid DeviceId, Guid AuctionId, Guid CustomerId);
public sealed record ReleaseDeviceCommand(Guid DeviceId, Guid AuctionId, Guid SessionId);
public sealed record DeactivateDeviceCommand(Guid DeviceId);

public sealed class IssueDeviceHandler(IDeviceAssignmentRepository repository)
{
    public Task<DeviceAssignmentResult> HandleAsync(IssueDeviceCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.DeviceId == Guid.Empty || command.AuctionId == Guid.Empty || command.CustomerId == Guid.Empty)
            return Task.FromResult(new DeviceAssignmentResult(DeviceAssignmentStatus.InvalidRequest));
        return repository.IssueAsync(command.DeviceId, command.AuctionId, command.CustomerId, cancellationToken);
    }
}

public sealed class ReleaseDeviceHandler(IDeviceAssignmentRepository repository)
{
    public Task<DeviceAssignmentResult> HandleAsync(ReleaseDeviceCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.DeviceId == Guid.Empty || command.AuctionId == Guid.Empty || command.SessionId == Guid.Empty)
            return Task.FromResult(new DeviceAssignmentResult(DeviceAssignmentStatus.InvalidRequest));
        return repository.ReleaseAsync(command.DeviceId, command.AuctionId, command.SessionId, cancellationToken);
    }
}

public sealed class DeactivateDeviceHandler(IDeviceAssignmentRepository repository)
{
    public Task<DeviceAssignmentResult> HandleAsync(DeactivateDeviceCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.DeviceId == Guid.Empty)
            return Task.FromResult(new DeviceAssignmentResult(DeviceAssignmentStatus.InvalidRequest));
        return repository.DeactivateAsync(command.DeviceId, cancellationToken);
    }
}
