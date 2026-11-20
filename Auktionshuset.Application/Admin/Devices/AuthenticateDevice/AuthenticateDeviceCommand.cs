using System;
using System.Collections.Generic;
using System.Text;

namespace Auktionshuset.Application.Admin.Devices.AuthenticateDevice
{
    public sealed class AuthenticateDeviceCommand
    {
        public required Guid DeviceId { get; init; }

        public required string Secret { get; init; }

    }
}
