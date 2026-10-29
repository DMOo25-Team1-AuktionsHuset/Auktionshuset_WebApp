using Auktionshuset.Application.Abstraction.Auth;
using Auktionshuset.Application.Admin.Devices.RegisterDevice;
using Auktionshuset.Domain.Entities;
using Moq;
using Xunit;

namespace Auktionshuset.Infrastructure.Test;

public sealed class RegisterDeviceHandlerTests
{
    // Kunstige værdier til mocks, ikke rigtige credentials.
    private const string TestSecret = "test-device-secret";
    private const string TestHash = "test-device-hash";

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 0)]
    [InlineData(true, -1)]
    public async Task InvalidInput_DoesNotGenerateOrSaveCredentials(
        bool hasAuctionHouse,
        int deviceNumber)
    {
        var repository =
            new Mock<IDeviceRepository>(MockBehavior.Strict);

        var secrets =
            new Mock<IDeviceSecretService>(MockBehavior.Strict);

        var handler = new RegisterDeviceHandler(
            repository.Object,
            secrets.Object);

        var command = new RegisterDeviceCommand(
            hasAuctionHouse ? Guid.NewGuid() : Guid.Empty,
            deviceNumber);

        RegisterDeviceResult result = await handler.HandleAsync(
            command,
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.NotEmpty(result.Errors);
        Assert.Null(result.Secret);

        repository.VerifyNoOtherCalls();
        secrets.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ValidInput_SavesHashAndReturnsOriginalSecret()
    {
        var repository =
            new Mock<IDeviceRepository>(MockBehavior.Strict);

        var secrets = CreateSecretService();

        Device? savedDevice = null;
        DeviceCredential? savedCredential = null;

        repository
            .Setup(r => r.AddAsync(
                It.IsAny<Device>(),
                It.IsAny<DeviceCredential>(),
                It.IsAny<CancellationToken>()))
            .Callback<Device, DeviceCredential, CancellationToken>(
                (device, credential, _) =>
                {
                    savedDevice = device;
                    savedCredential = credential;
                })
            .Returns(Task.CompletedTask);

        var handler = new RegisterDeviceHandler(
            repository.Object,
            secrets.Object);

        Guid auctionHouseId = Guid.NewGuid();

        var command = new RegisterDeviceCommand(
            auctionHouseId,
            42);

        RegisterDeviceResult result = await handler.HandleAsync(
            command,
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Empty(result.Errors);
        Assert.Equal(TestSecret, result.Secret);

        Assert.NotNull(savedDevice);
        Assert.NotNull(savedCredential);

        Assert.NotEqual(Guid.Empty, savedDevice.DeviceId);
        Assert.Equal(result.DeviceId, savedDevice.DeviceId);
        Assert.Equal(auctionHouseId, savedDevice.AuctionHouseId);
        Assert.Equal(42, savedDevice.DeviceNumber);

        Assert.Equal(savedDevice.DeviceId, savedCredential.DeviceId);
        Assert.Same(savedDevice, savedCredential.Device);

        Assert.Equal(TestHash, savedCredential.SecretHash);
        Assert.NotEqual(TestSecret, savedCredential.SecretHash);
        Assert.True(savedCredential.IsEnabled);
        Assert.Equal(1, savedCredential.CredentialVersion);

        repository.Verify(r => r.AddAsync(
            It.IsAny<Device>(),
            It.IsAny<DeviceCredential>(),
            CancellationToken.None), Times.Once);

        secrets.VerifyAll();
    }

    [Fact]
    public async Task SaveFailure_DoesNotReturnSuccessfulRegistration()
    {
        var repository =
            new Mock<IDeviceRepository>(MockBehavior.Strict);

        var secrets = CreateSecretService();

        var save = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        repository
            .Setup(r => r.AddAsync(
                It.IsAny<Device>(),
                It.IsAny<DeviceCredential>(),
                It.IsAny<CancellationToken>()))
            .Returns(save.Task);

        var handler = new RegisterDeviceHandler(
            repository.Object,
            secrets.Object);

        var command = new RegisterDeviceCommand(
            Guid.NewGuid(),
            42);

        Task<RegisterDeviceResult> registration =
            handler.HandleAsync(
                command,
                CancellationToken.None);

        // Handleren skal vente på gemningen.
        Assert.False(registration.IsCompleted);

        save.SetException(
            new InvalidOperationException(
                "Simulated database failure."));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => registration);
    }

    [Fact]
    public void Result_ToString_DoesNotContainSecret()
    {
        RegisterDeviceResult result =
            RegisterDeviceResult.Created(
                Guid.NewGuid(),
                Guid.NewGuid(),
                42,
                TestSecret);

        Assert.DoesNotContain(
            TestSecret,
            result.ToString()!);
    }

    private static Mock<IDeviceSecretService> CreateSecretService()
    {
        var secrets =
            new Mock<IDeviceSecretService>(MockBehavior.Strict);

        secrets
            .Setup(s => s.GenerateSecret())
            .Returns(TestSecret);

        secrets
            .Setup(s => s.HashSecret(TestSecret))
            .Returns(TestHash);

        return secrets;
    }
}
