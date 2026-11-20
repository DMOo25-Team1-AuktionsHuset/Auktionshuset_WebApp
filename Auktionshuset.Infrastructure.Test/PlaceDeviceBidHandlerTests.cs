using Auktionshuset.Application.Abstraction.Auction;
using Auktionshuset.Application.Abstraction.Auth;
using Auktionshuset.Application.Admin.Devices;
using Auktionshuset.Application.Auction;
using Auktionshuset.Application.Devices.AuthenticateDevice;
using Auktionshuset.Domain.Entities;
using Moq;

namespace Auktionshuset.Infrastructure.Test;

public sealed class PlaceDeviceBidHandlerTests
{
    [Fact]
    public async Task AuthenticatedBidAndRetryForwardIdenticalPayloadAndCorrelateReceipts()
    {
        var f = new Fixture();
        var bidId = Guid.NewGuid();
        var cancellationToken = new CancellationTokenSource().Token;
        f.Store.SetupSequence(s => s.PlaceBidAsync(It.IsAny<PlaceBidCommand>(), cancellationToken))
            .ReturnsAsync(new PlaceBidResult(true, false, bidId, 7, 123.45m, null, 43))
            .ReturnsAsync(new PlaceBidResult(true, true, bidId, 7, 123.45m, null, 43));

        var first = await f.Handler.HandleAsync(f.Command, cancellationToken);
        var retry = await f.Handler.HandleAsync(f.Command, cancellationToken);

        var expected = new PlaceBidCommand(f.Command.AuctionLotId, f.Session.CustomerId,
            f.Session.SessionId, f.Command.RequestId, f.Command.Amount, f.Command.AuctionRevision,
            f.Command.DeviceId, 1);
        f.Store.Verify(s => s.PlaceBidAsync(expected, cancellationToken), Times.Exactly(2));
        f.Repository.Verify(r => r.GetCredentialAsync(f.Command.DeviceId, cancellationToken), Times.Exactly(4));
        f.Secrets.Verify(s => s.VerifySecret(f.Command.Secret, "hash"), Times.Exactly(2));
        Assert.Equal(f.Command.DeviceId, first.DeviceId);
        Assert.Equal(f.Session.SessionId, first.SessionId);
        Assert.Equal(f.Session.AuctionId, first.AuctionId);
        Assert.Equal(f.Command.AuctionLotId, first.AuctionLotId);
        Assert.Equal(f.Command.AuctionRevision, first.RequestedAuctionRevision);
        Assert.Equal(f.Command.RequestId, first.RequestId);
        Assert.True(first.Accepted);
        Assert.False(first.Duplicate);
        Assert.Equal(bidId, first.BidId);
        Assert.Equal(7, first.SequenceNumber);
        Assert.Equal(123.45m, first.CurrentPrice);
        Assert.Equal(43, first.AuctionRevision);
        Assert.Null(first.ErrorCode);
        Assert.Equal(first with { Duplicate = true }, retry);
    }

    [Fact]
    public async Task WrongSecretOrDeviceIsRejectedWithoutPlacingBid()
    {
        var f = new Fixture();
        var wrongSecret = await f.Handler.HandleAsync(f.Command with { Secret = new string('b', 64) }, default);
        var wrongDevice = await f.Handler.HandleAsync(f.Command with { DeviceId = Guid.NewGuid() }, default);
        Assert.Equal("InvalidDeviceCredentials", wrongSecret.ErrorCode);
        Assert.Equal("InvalidDeviceCredentials", wrongDevice.ErrorCode);
        Assert.False(wrongSecret.Accepted);
        Assert.Equal(f.Command.RequestId, wrongSecret.RequestId);
        f.Store.Verify(s => s.PlaceBidAsync(It.IsAny<PlaceBidCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task MissingStaleOrWrongDeviceSessionIsRejected()
    {
        var f = new Fixture();
        var stale = await f.Handler.HandleAsync(f.Command with { SessionId = Guid.NewGuid() }, default);
        f.Repository.Setup(r => r.GetActiveSessionAsync(f.Command.DeviceId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ActiveDeviceSession?)null);
        var missing = await f.Handler.HandleAsync(f.Command, default);
        f.Repository.Setup(r => r.GetActiveSessionAsync(f.Command.DeviceId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(f.Session with { DeviceId = Guid.NewGuid() });
        var wrongDevice = await f.Handler.HandleAsync(f.Command, default);
        Assert.Equal("InvalidDeviceSession", stale.ErrorCode);
        Assert.Equal("InvalidDeviceSession", missing.ErrorCode);
        Assert.Equal("InvalidDeviceSession", wrongDevice.ErrorCode);
        Assert.Equal(f.Command.SessionId, missing.SessionId);
        f.Store.Verify(s => s.PlaceBidAsync(It.IsAny<PlaceBidCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task StaleSessionRevisionIsRejectedWithoutPlacingBid()
    {
        var f = new Fixture();
        var receipt = await f.Handler.HandleAsync(f.Command with { SessionRevision = f.Session.SessionRevision + 1 }, default);
        Assert.Equal("InvalidDeviceSession", receipt.ErrorCode);
        f.Store.Verify(s => s.PlaceBidAsync(It.IsAny<PlaceBidCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AuctionMismatchIsRejectedInsteadOfTrustingClientAuction()
    {
        var f = new Fixture();
        var command = f.Command with { AuctionId = Guid.NewGuid() };
        var receipt = await f.Handler.HandleAsync(command, default);
        Assert.False(receipt.Accepted);
        Assert.Equal("AuctionContextChanged", receipt.ErrorCode);
        Assert.Equal(command.AuctionId, receipt.AuctionId);
        Assert.Equal(command.RequestId, receipt.RequestId);
        f.Store.Verify(s => s.PlaceBidAsync(It.IsAny<PlaceBidCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task StoreRejectionIsReturnedWithOriginalContext()
    {
        var f = new Fixture();
        f.Store.Setup(s => s.PlaceBidAsync(It.IsAny<PlaceBidCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlaceBidResult(false, true, null, null, 100m, "AuctionContextChanged"));
        var receipt = await f.Handler.HandleAsync(f.Command, default);
        Assert.False(receipt.Accepted);
        Assert.True(receipt.Duplicate);
        Assert.Null(receipt.BidId);
        Assert.Null(receipt.SequenceNumber);
        Assert.Equal(100m, receipt.CurrentPrice);
        Assert.Equal("AuctionContextChanged", receipt.ErrorCode);
        Assert.Equal(f.Command.RequestId, receipt.RequestId);
        Assert.Equal(f.Session.AuctionId, receipt.AuctionId);
        Assert.Equal(f.Command.AuctionLotId, receipt.AuctionLotId);
        Assert.Equal(f.Command.AuctionRevision, receipt.RequestedAuctionRevision);
    }

    private sealed class Fixture
    {
        public Mock<IDeviceRepository> Repository { get; } = new();
        public Mock<IDeviceSecretService> Secrets { get; } = new();
        public Mock<IPlaceBidStore> Store { get; } = new();
        public ActiveDeviceSession Session { get; }
        public PlaceDeviceBidCommand Command { get; }
        public PlaceDeviceBidHandler Handler { get; }

        public Fixture()
        {
            var deviceId = Guid.NewGuid();
            Session = new ActiveDeviceSession(deviceId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
            Command = new PlaceDeviceBidCommand(deviceId, new string('a', 64), Session.SessionId,
                Session.AuctionId, Guid.NewGuid(), 42, Guid.NewGuid(), 123.45m);
            var credential = new DeviceCredential
            {
                DeviceId = deviceId, IsEnabled = true, CredentialVersion = 1, SecretHash = "hash",
                Device = new Device { DeviceId = deviceId, AuctionHouseId = Guid.NewGuid(), DeviceNumber = 1, Status = "Available" }
            };
            Repository.Setup(r => r.GetCredentialAsync(deviceId, It.IsAny<CancellationToken>())).ReturnsAsync(credential);
            Repository.Setup(r => r.GetActiveSessionAsync(deviceId, null, It.IsAny<CancellationToken>())).ReturnsAsync(Session);
            Secrets.Setup(s => s.VerifySecret(Command.Secret, "hash")).Returns(true);
            Handler = new PlaceDeviceBidHandler(new AuthenticateDeviceHandler(Repository.Object, Secrets.Object), new PlaceBidHandler(Store.Object));
        }
    }
}
