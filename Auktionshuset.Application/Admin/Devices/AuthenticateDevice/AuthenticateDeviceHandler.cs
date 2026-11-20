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
        CancellationToken cancellationToken) =>
        await VerifyAsync(command, false, cancellationToken);

    public Task<AuthenticateDeviceResult?> VerifySetupAsync(
        AuthenticateDeviceCommand command, CancellationToken cancellationToken) =>
        VerifyAsync(command, true, cancellationToken);

    private async Task<AuthenticateDeviceResult?> VerifyAsync(
        AuthenticateDeviceCommand command, bool allowPendingSetup,
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

        if (credential is null || !Eligible(credential, allowPendingSetup))
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

        ActiveDeviceSession? session = allowPendingSetup ? null : await deviceRepository.GetActiveSessionAsync(
            credential.DeviceId, null, cancellationToken);

        // The credential can be disabled or replaced while the secret is being verified.
        DeviceCredential? currentCredential = await deviceRepository.GetCredentialAsync(command.DeviceId, cancellationToken);
        if (currentCredential is null || !Eligible(currentCredential, allowPendingSetup) ||
            currentCredential.CredentialVersion != credential.CredentialVersion ||
            currentCredential.SecretHash != credential.SecretHash ||
            currentCredential.SessionRevision != credential.SessionRevision ||
            (session is not null && session.SessionRevision != currentCredential.SessionRevision))
        {
            return null;
        }

        return new AuthenticateDeviceResult(
            credential.DeviceId,
            credential.Device.DeviceNumber,
            credential.CredentialVersion)
        {
            Session = session
        };
    }

    private static bool Eligible(DeviceCredential credential, bool allowPendingSetup) =>
        credential.IsEnabled || (allowPendingSetup &&
            (credential.Device.Status.StartsWith("Provisioning:") ||
             credential.Device.Status.StartsWith("RepairPending:") ||
             credential.Device.Status == "Provisioning"));
}
