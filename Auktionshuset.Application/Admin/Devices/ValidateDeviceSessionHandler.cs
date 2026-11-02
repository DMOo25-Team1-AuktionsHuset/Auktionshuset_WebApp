using Auktionshuset.Application.Abstraction.Auth;

namespace Auktionshuset.Application.Admin.Devices;

public sealed record ValidateDeviceSessionCommand(Guid DeviceId, Guid SessionId);

public sealed class ValidateDeviceSessionHandler(IDeviceRepository repository)
{
    public Task<ActiveDeviceSession?> HandleAsync(ValidateDeviceSessionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();
        if (command.DeviceId == Guid.Empty || command.SessionId == Guid.Empty)
            return Task.FromResult<ActiveDeviceSession?>(null);

        return repository.GetActiveSessionAsync(command.DeviceId, command.SessionId, cancellationToken);
    }
}
