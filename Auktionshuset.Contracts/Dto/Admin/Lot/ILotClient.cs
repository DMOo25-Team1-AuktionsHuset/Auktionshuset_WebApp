using Auktionshuset.Contracts.Dto.Admin.Lot.UpdateLot;
﻿using Auktionshuset.Contracts.Dto.Admin.Lot.CreateLot;
using System;
using System.Collections.Generic;
using System.Text;
using Auktionshuset.Contracts.Dto.Admin.Lot.DeleteLot;

namespace Auktionshuset.Contracts.Dto.Admin.Lot {
    public interface ILotClient {
        Task LotCreatedAsync(CreateLotNotification notification);
        Task LotUpdatedAsync(UpdateLotNotification notification);

        Task LotDeletedAsync(DeleteLotNotification notification);
    }
}
