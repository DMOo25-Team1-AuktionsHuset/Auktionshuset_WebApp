using Auktionshuset.Contracts.Dto.Admin.Lot.UpdateLot;
﻿using Auktionshuset.Contracts.Dto.Admin.Lot.CreateLot;
using Auktionshuset.Contracts.Dto.Admin.Lot.DeleteLot;
using System;
using System.Collections.Generic;
using System.Text;


namespace Auktionshuset.Contracts.Dto.Admin.Lot {
    public interface ILotClient {
        /// <summary>
        /// Notifies connected clients that a lot was created.
        /// </summary>
        /// <param name="notification">The details of the newly created lot.</param>
        Task LotCreatedAsync(CreateLotNotification notification);

        /// <summary>
        /// Notifies connected clients that a lot was updated.
        /// </summary>
        /// <param name="notification">The updated details of the lot.</param>
        Task LotUpdatedAsync(UpdateLotNotification notification);

        /// <summary>
        /// Notifies connected clients that a lot was deleted.
        /// </summary>
        /// <param name="notification">The identifier of the deleted lot.</param>
        Task LotDeletedAsync(DeleteLotNotification notification);
    }
}
