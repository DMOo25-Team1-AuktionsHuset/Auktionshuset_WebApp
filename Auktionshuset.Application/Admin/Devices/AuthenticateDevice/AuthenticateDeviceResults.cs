using System;
using System.Collections.Generic;
using System.Text;

namespace Auktionshuset.Application.Admin.Devices.AuthenticateDevice
{
    public sealed record AuthenticateDeviceResults(
        Guid DeviceId,
        Guid AuctionHouseId,
        int DeviceNumber,
        int CredentialVersion);
}
