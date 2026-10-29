using System;
using System.Collections.Generic;
using System.Text;
using Auktionshuset.Application.Abstraction.Auth;
using Auktionshuset.Domain.Entities;

namespace Auktionshuset.Application.Admin.Devices.RegisterDevice
{
    public sealed class RegisterDeviceHandler(
        IDeviceRepository deviceRepository,
        IDeviceSecretService secretService)
    {
        public async Task<RegisterDeviceResult> HandleAsync(
            RegisterDeviceCommand command,
            CancellationToken cancellationToken)

        {
            ArgumentNullException.ThrowIfNull(command);
            cancellationToken.ThrowIfCancellationRequested();

            var errors = new List<string>();

            if (command.AuctionHouseId == Guid.Empty)
            {
                errors.Add("Auktionshus skal angives");
            }

            if (command.DeviceNumber <= 0)
            {
                errors.Add("Enhedsnummer skal være større end nul.");
            }
            
            if (errors.Count > 0)
            {
                return RegisterDeviceResult.Invalid(
                    errors.ToArray());
            }

            Guid deviceId = Guid.NewGuid();

            string secret = secretService.GenerateSecret();
            string secretHash = secretService.HashSecret(secret);

            var device = new Device
            {
                DeviceId = deviceId,
                AuctionHouseId = command.AuctionHouseId,
                DeviceNumber = command.DeviceNumber,
                Status = "Available"

            };

            var credential = new DeviceCredential
            {
                DeviceId = deviceId,
                Device = device,
                SecretHash = secretHash,
                IsEnabled = true,
                CredentialVersion = 1
            };

            await deviceRepository.AddAsync(
                device,
                credential,
                cancellationToken);

            return RegisterDeviceResult.Created(
                device.DeviceId,
                device.AuctionHouseId,
                device.DeviceNumber,
                secret);
        }
    }
}
