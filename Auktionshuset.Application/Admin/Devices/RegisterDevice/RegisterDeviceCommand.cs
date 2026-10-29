using System;
using System.Collections.Generic;
using System.Text;

namespace Auktionshuset.Application.Admin.Devices.RegisterDevice
{
    public sealed record RegisterDeviceCommand(
        Guid AuctionHouseId,
        int DeviceNumber);
}
