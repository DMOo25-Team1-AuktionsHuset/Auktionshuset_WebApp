using Auktionshuset.Application.Abstraction.Auth;
using Auktionshuset.Application.Admin.Devices.AuthenticateDevice;
using Auktionshuset.Application.Devices.AuthenticateDevice;
using Auktionshuset.Domain.Entities;
using Moq;
using Xunit;

namespace Auktionshuset.Infrastructure.Test;

public sealed class AuthenticateDeviceHandlerTests
{
    private readonly Guid _deviceId = Guid.NewGuid();

    // Kunstige testværdier. Verificeringen styres af en mock.
    private static readonly string Secret = new string('A', 64);
    private static readonly string StoredHash = new string('C', 64);

    private readonly Mock<IDeviceRepository> _repository =
        new(MockBehavior.Strict);

    private readonly Mock<IDeviceSecretService> _secrets =
        new(MockBehavior.Strict);

    private readonly AuthenticateDeviceHandler _handler;

    public AuthenticateDeviceHandlerTests()
    {
        _handler = new AuthenticateDeviceHandler(
            _repository.Object,
            _secrets.Object);
    }

    [Fact]
    public async Task ValidCredentials_ReturnDeviceIdentity()
    {
        _repository
            .Setup(r => r.GetCredentialAsync(
                _deviceId,
                CancellationToken.None))
            .ReturnsAsync(CreateCredential());

        _secrets
            .Setup(s => s.VerifySecret(Secret, StoredHash))
            .Returns(true);

        var command = new AuthenticateDeviceCommand
        {
            DeviceId = _deviceId,
            Secret = Secret
        };

        AuthenticateDeviceResult? result =
            await _handler.HandleAsync(
                command,
                CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(_deviceId, result.DeviceId);
        Assert.Equal(42, result.DeviceNumber);
        Assert.Equal(3, result.CredentialVersion);
    }

    [Fact]
    public async Task IncorrectSecret_ReturnsNull()
    {
        string incorrectSecret = new string('B', 64);

        _repository
            .Setup(r => r.GetCredentialAsync(
                _deviceId,
                CancellationToken.None))
            .ReturnsAsync(CreateCredential());

        _secrets
            .Setup(s => s.VerifySecret(
                incorrectSecret,
                StoredHash))
            .Returns(false);

        var command = new AuthenticateDeviceCommand
        {
            DeviceId = _deviceId,
            Secret = incorrectSecret
        };

        AuthenticateDeviceResult? result =
            await _handler.HandleAsync(
                command,
                CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task UnknownDevice_ReturnsNull()
    {
        _repository
            .Setup(r => r.GetCredentialAsync(
                _deviceId,
                CancellationToken.None))
            .ReturnsAsync((DeviceCredential?)null);

        var command = new AuthenticateDeviceCommand
        {
            DeviceId = _deviceId,
            Secret = Secret
        };

        AuthenticateDeviceResult? result =
            await _handler.HandleAsync(
                command,
                CancellationToken.None);

        Assert.Null(result);

        _secrets.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task DisabledCredentials_ReturnNull()
    {
        _repository
            .Setup(r => r.GetCredentialAsync(
                _deviceId,
                CancellationToken.None))
            .ReturnsAsync(CreateCredential(isEnabled: false));

        var command = new AuthenticateDeviceCommand
        {
            DeviceId = _deviceId,
            Secret = Secret
        };

        AuthenticateDeviceResult? result =
            await _handler.HandleAsync(
                command,
                CancellationToken.None);

        Assert.Null(result);

        _secrets.VerifyNoOtherCalls();
    }

    private DeviceCredential CreateCredential(
        bool isEnabled = true)
    {
        var device = new Device
        {
            DeviceId = _deviceId,
            AuctionHouseId = Guid.NewGuid(),
            DeviceNumber = 42,
            Status = "Available"
        };

        return new DeviceCredential
        {
            DeviceId = _deviceId,
            Device = device,
            SecretHash = StoredHash,
            IsEnabled = isEnabled,
            CredentialVersion = 3
        };
    }
}
