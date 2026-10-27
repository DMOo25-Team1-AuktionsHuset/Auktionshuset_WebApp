using Auktionshuset.Contracts.Dto.Admin.Employee;
using Auktionshuset.Contracts.Dto.Admin.Employee.CreateEmployee;
using Auktionshuset.Contracts.Dto.Admin.Employee.DeleteEmployee;
using Auktionshuset.Contracts.Dto.Admin.Employee.UpdateEmployee;
using Auktionshuset.Contracts.Dto.Admin.Lot;
using Auktionshuset.Contracts.Dto.Admin.Lot.CreateLot;
using Auktionshuset.Contracts.Dto.Admin.Lot.DeleteLot;
using Auktionshuset.Contracts.Dto.Admin.Lot.UpdateLot;
using Auktionshuset.Security;
using Microsoft.AspNetCore.SignalR.Client;

namespace Auktionshuset.Services
{
    public class LotRealtimeService : RealtimeServiceBase
    {
        public LotRealtimeService(
            IConfiguration configuration,
            BackendTokenAccessor tokenAccessor,
            ILogger<LotRealtimeService> logger)
            : base(
                  "hubs/lot",
                  configuration,
                  tokenAccessor,
                  logger)
        {
        }

        protected override void ConfigureSubscriptions(
            HubConnection hubConnection)
        {
            InvalidateOn<CreateLotNotification>(
                hubConnection,
                nameof(ILotClient.LotCreatedAsync));

            InvalidateOn<UpdateLotNotification>(
                hubConnection,
                nameof(ILotClient.LotUpdatedAsync));

            InvalidateOn<DeleteLotNotification>(
                hubConnection,
                nameof(ILotClient.LotDeletedAsync));
        }
    }
}
