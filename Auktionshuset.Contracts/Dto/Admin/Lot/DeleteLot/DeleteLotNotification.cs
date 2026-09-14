using System;
using System.Collections.Generic;
using System.Text;

namespace Auktionshuset.Contracts.Dto.Admin.Lot.DeleteLot
{
    public sealed record DeleteLotNotification(Guid LotId);
}
