using Auktionshuset.Application.Abstraction.Auth;
using Auktionshuset.Application.Admin.Devices.AuthenticateDevice;
using Auktionshuset.Domain.Entities;

namespace Auktionshuset.Application.Devices.AuthenticateDevice;

public sealed class AuthenticateDeviceHandler(
    IDeviceRepository deviceRepository,
    IDeviceSecretService secretService)
{
    private const int SecretLength = 64;

    public async Task<AuthenticateDeviceResult?> HandleAsync(
        AuthenticateDeviceCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();

        if (
            command.DeviceId == Guid.Empty ||
            string.IsNullOrWhiteSpace(command.Secret) ||
            command.Secret.Length != SecretLength
        )
        {
            return null;
        }

        DeviceCredential? credential =
            await deviceRepository.GetCredentialAsync(
                command.DeviceId,
                cancellationToken);

        if (credential is null || !credential.IsEnabled)
        {
            return null;
        }

        bool secretIsValid = secretService.VerifySecret(
            command.Secret,
            credential.SecretHash);

        if (!secretIsValid)
        {
            return null;
        }

        return new AuthenticateDeviceResult(
            credential.DeviceId,
            credential.Device.AuctionHouseId,
            credential.Device.DeviceNumber,
            credential.CredentialVersion);
    }
}
