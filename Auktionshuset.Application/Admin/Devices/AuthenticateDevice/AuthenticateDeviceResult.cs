using System;
using System.Collections.Generic;
using System.Text;
using Auktionshuset.Application.Abstraction.Auth;

namespace Auktionshuset.Application.Admin.Devices.AuthenticateDevice
{
    public sealed record AuthenticateDeviceResult(
        Guid DeviceId,
        int DeviceNumber,
        int CredentialVersion)
    {
        public ActiveDeviceSession? Session { get; init; }
    }
}
