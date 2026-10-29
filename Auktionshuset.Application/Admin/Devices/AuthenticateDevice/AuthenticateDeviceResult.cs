using System;
using System.Collections.Generic;
using System.Text;

namespace Auktionshuset.Application.Admin.Devices.AuthenticateDevice
{
    public sealed record AuthenticateDeviceResult(
        Guid DeviceId,
        int DeviceNumber,
        int CredentialVersion);
}
