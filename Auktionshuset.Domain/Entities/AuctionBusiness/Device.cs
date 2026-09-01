using System;
using System.Collections.Generic;
using System.Text;
using System.ComponentModel.DataAnnotations;

namespace Auktionshuset.Domain.Entities.AuctionBusiness
{
    public class Device
    {
        public required Guid DeviceId { get; set; }
        public required int DeviceNumber { get; set; }
    }
}
