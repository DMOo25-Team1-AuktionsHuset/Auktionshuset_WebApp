using System;
using System.Collections.Generic;
using System.Text;
using Auktionshuset.Domain.Entities;

namespace Auktionshuset.Application.Abstraction.Admin.Lots {
    public interface ILotRepository {
        Task AddAsync(Lot lot, CancellationToken cancellationToken);
    }
}
