using Auktionshuset.Domain.Entities;

namespace Auktionshuset.Application.Abstraction.Auth
{
    public sealed record ActiveDeviceSession(Guid DeviceId, Guid SessionId, Guid CustomerId, Guid AuctionId, long SessionRevision = 0);

    public interface IDeviceRepository
    {
        Task AddAsync(
            Device device,
            DeviceCredential credential,
            CancellationToken cancellationToken);

        Task<DeviceCredential?> GetCredentialAsync(
            Guid deviceId,
            CancellationToken cancellationToken);

        Task<ActiveDeviceSession?> GetActiveSessionAsync(
            Guid deviceId,
            Guid? sessionId,
            CancellationToken cancellationToken);
    }
}
