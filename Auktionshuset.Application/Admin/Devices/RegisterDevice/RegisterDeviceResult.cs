using System;
using System.Collections.Generic;
using System.Text;

namespace Auktionshuset.Application.Admin.Devices.RegisterDevice
{
    public sealed class RegisterDeviceResult
    {
        private RegisterDeviceResult()
        {

        }

        public Guid DeviceId { get; private init; }

        public Guid AuctionHouseId { get; private init; }
        public int DeviceNumber { get; private init; }
        public string? Secret { get; private init; }
        public IReadOnlyCollection<string> Errors
        { get; private init; } = [];
        public bool Succeeded =>
            Secret is not null && Errors.Count == 0;

        public static RegisterDeviceResult Created(
            Guid deviceId,
            Guid auctionHouseId,
            int deviceNumber,
            string secret)
        {
            return new RegisterDeviceResult
            {
                DeviceId = deviceId,
                AuctionHouseId = auctionHouseId,
                DeviceNumber = deviceNumber,
                Secret = secret
            };
        }

        public static RegisterDeviceResult Invalid(
            IReadOnlyCollection<string> errors)
        {
            return new RegisterDeviceResult
            {
                Errors = errors
            };
        }
    }
}
