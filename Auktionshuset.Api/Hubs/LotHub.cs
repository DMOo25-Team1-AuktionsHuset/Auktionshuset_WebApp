using Auktionshuset.Contracts.Dto.Admin.Lot;
using Microsoft.AspNetCore.SignalR;

namespace Auktionshuset.Api.Hubs
{
    public class LotHub : Hub<ILotClient>
    {
    }
}
