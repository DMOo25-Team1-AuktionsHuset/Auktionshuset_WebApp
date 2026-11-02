using Auktionshuset.Application.Abstraction.Auth;
using Auktionshuset.Application.Admin.Devices;
using Auktionshuset.Application.Admin.Devices.AuthenticateDevice;
using Auktionshuset.Application.Devices.AuthenticateDevice;
using Auktionshuset.Domain.Entities;
using Moq;
using Xunit;

namespace Auktionshuset.Infrastructure.Test;

public sealed class DeviceSessionAccessTests
{
    [Fact]
    public async Task AuthenticationReturnsCurrentSessionButDoesNotRequireOne()
    {
        Guid deviceId = Guid.NewGuid();
        var credential = new DeviceCredential
        {
            DeviceId = deviceId, IsEnabled = true, CredentialVersion = 1, SecretHash = "hash", SessionRevision = 7,
            Device = new Device { DeviceId = deviceId, AuctionHouseId = Guid.NewGuid(), DeviceNumber = 1, Status = "Available" }
        };
        var session = new ActiveDeviceSession(deviceId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 7);
        var repository = new Mock<IDeviceRepository>();
        repository.Setup(r => r.GetCredentialAsync(deviceId, It.IsAny<CancellationToken>())).ReturnsAsync(credential);
        repository.SetupSequence(r => r.GetActiveSessionAsync(deviceId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session).ReturnsAsync((ActiveDeviceSession?)null);
        var secrets = new Mock<IDeviceSecretService>();
        secrets.Setup(s => s.VerifySecret(It.IsAny<string>(), "hash")).Returns(true);
        var handler = new AuthenticateDeviceHandler(repository.Object, secrets.Object);
        var command = new AuthenticateDeviceCommand { DeviceId = deviceId, Secret = new string('a', 64) };

        Assert.Equal(session, (await handler.HandleAsync(command, default))!.Session);
        Assert.Null((await handler.HandleAsync(command, default))!.Session);
    }

    [Fact]
    public async Task AuthenticationRejectsCredentialDisabledDuringVerification()
    {
        Guid deviceId = Guid.NewGuid();
        var credential = new DeviceCredential
        {
            DeviceId = deviceId, IsEnabled = true, CredentialVersion = 1, SecretHash = "hash",
            Device = new Device { DeviceId = deviceId, AuctionHouseId = Guid.NewGuid(), DeviceNumber = 1, Status = "Available" }
        };
        var repository = new Mock<IDeviceRepository>();
        repository.SetupSequence(r => r.GetCredentialAsync(deviceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(credential).ReturnsAsync((DeviceCredential?)null);
        var secrets = new Mock<IDeviceSecretService>();
        secrets.Setup(s => s.VerifySecret(It.IsAny<string>(), "hash")).Returns(true);
        var handler = new AuthenticateDeviceHandler(repository.Object, secrets.Object);

        Assert.Null(await handler.HandleAsync(new AuthenticateDeviceCommand { DeviceId = deviceId, Secret = new string('a', 64) }, default));
    }

    [Fact]
    public async Task AuthenticationRejectsSessionChangedDuringVerification()
    {
        Guid deviceId = Guid.NewGuid();
        var device = new Device { DeviceId = deviceId, AuctionHouseId = Guid.NewGuid(), DeviceNumber = 1, Status = "Available" };
        var original = new DeviceCredential { DeviceId = deviceId, Device = device, IsEnabled = true, SecretHash = "hash", SessionRevision = 1 };
        var changed = new DeviceCredential { DeviceId = deviceId, Device = device, IsEnabled = true, SecretHash = "hash", SessionRevision = 2 };
        var repository = new Mock<IDeviceRepository>();
        repository.SetupSequence(r => r.GetCredentialAsync(deviceId, It.IsAny<CancellationToken>())).ReturnsAsync(original).ReturnsAsync(changed);
        repository.Setup(r => r.GetActiveSessionAsync(deviceId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ActiveDeviceSession(deviceId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1));
        var secrets = new Mock<IDeviceSecretService>();
        secrets.Setup(s => s.VerifySecret(It.IsAny<string>(), "hash")).Returns(true);
        var handler = new AuthenticateDeviceHandler(repository.Object, secrets.Object);
        Assert.Null(await handler.HandleAsync(new AuthenticateDeviceCommand { DeviceId = deviceId, Secret = new string('a', 64) }, default));
    }

    [Fact]
    public async Task ValidationRequiresBothIdentifiersAndReturnsRepositoryContext()
    {
        Guid deviceId = Guid.NewGuid();
        Guid sessionId = Guid.NewGuid();
        var session = new ActiveDeviceSession(deviceId, sessionId, Guid.NewGuid(), Guid.NewGuid());
        var repository = new Mock<IDeviceRepository>(MockBehavior.Strict);
        repository.Setup(r => r.GetActiveSessionAsync(deviceId, sessionId, It.IsAny<CancellationToken>())).ReturnsAsync(session);
        var handler = new ValidateDeviceSessionHandler(repository.Object);

        Assert.Null(await handler.HandleAsync(new(Guid.Empty, sessionId), default));
        Assert.Null(await handler.HandleAsync(new(deviceId, Guid.Empty), default));
        Assert.Equal(session, await handler.HandleAsync(new(deviceId, sessionId), default));
        repository.Verify(r => r.GetActiveSessionAsync(deviceId, sessionId, It.IsAny<CancellationToken>()), Times.Once);
    }
}
