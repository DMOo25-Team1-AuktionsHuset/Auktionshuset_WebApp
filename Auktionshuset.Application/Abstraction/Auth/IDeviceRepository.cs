using Auktionshuset.Domain.Entities;

namespace Auktionshuset.Application.Abstraction.Auth
{
    public interface IDeviceRepository
    {
        Task AddAsync(
            Device device,
            DeviceCredential credential,
            CancellationToken cancellationToken);

        Task<DeviceCredential?> GetCredentialAsync(
            Guid deviceId,
            CancellationToken cancellationToken);
    }
}
