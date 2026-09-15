using Auktionshuset.Contracts.Dto.Admin.Lot.UpdateLot;
﻿using Auktionshuset.Contracts.Dto.Admin.Lot.CreateLot;
using System;
using System.Collections.Generic;
using System.Text;

namespace Auktionshuset.Contracts.Dto.Admin.Lot {
    public interface ILotClient {
        Task LotCreatedAsync(CreateLotNotification notification);
        Task LotUpdatedAsync(UpdateLotNotification notification);
    }
}
