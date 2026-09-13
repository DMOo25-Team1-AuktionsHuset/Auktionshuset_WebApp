using Microsoft.AspNetCore.SignalR;
using Auktionshuset.Contracts.Dto.Admin.Lot;

namespace Auktionshuset.Api.Hubs {
    public class LotHub : Hub<ILotClient> {
    }
}
