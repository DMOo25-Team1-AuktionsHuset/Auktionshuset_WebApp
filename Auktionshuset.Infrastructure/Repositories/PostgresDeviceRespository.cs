using Auktionshuset.Application.Abstraction.Auth;
using Auktionshuset.Domain.Entities;
using Auktionshuset.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Auktionshuset.Infrastructure.Repositories
{
    public sealed class PostgresDeviceRepository(
        AHDBContext db) : IDeviceRepository
    {
        public async Task AddAsync(
            Device device,
            DeviceCredential credential,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(device);
            ArgumentNullException.ThrowIfNull(credential);

            if (device.DeviceId != credential.DeviceId)

            {
                throw new ArgumentException(
                    "Device and credential must have the same device id",
                    nameof(credential));
            }

            credential.Device = device;

            db.Set<Device>().Add(device);
            db.DeviceCredentials.Add(credential);

            await db.SaveChangesAsync(cancellationToken);
        }

        public Task<DeviceCredential?> GetCredentialAsync(
            Guid deviceId,
            CancellationToken cancellationToken)
        {
            return db.DeviceCredentials
                .AsNoTracking()
                .Include(credential => credential.Device)
                .SingleOrDefaultAsync(
                    credential => credential.DeviceId == deviceId,
                    cancellationToken);
        }
    }
}
