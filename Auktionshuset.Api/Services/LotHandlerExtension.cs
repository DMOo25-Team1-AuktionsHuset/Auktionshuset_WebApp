using Auktionshuset.Api.Events.Admin.Lot;
using Auktionshuset.Application.Admin.Lots;
using Auktionshuset.Application.Admin.Lots.CreateLot;
using Auktionshuset.Application.Admin.Lots.DeleteLot;
using Auktionshuset.Application.Admin.Lots.UpdateLot;
using Auktionshuset.Application.EventHandling;

namespace Auktionshuset.Api.Services
{
    internal static class LotHandlerExtension
    {
        internal static IServiceCollection AddLotHandler(
            this IServiceCollection services)
        {
            services.AddScoped<CreateLotHandler>();
            services.AddScoped<UpdateLotHandler>();
            services.AddScoped<GetLotsHandler>();
            services.AddScoped<DeleteLotHandler>();

            services.AddScoped<
                IIntegrationEventHandler<LotCreatedIntegrationEvent>,
                CreateLotRealTimeHandler>();

            services.AddScoped<
                IIntegrationEventHandler<LotUpdatedIntegrationEvent>,
                UpdateLotRealTimeHandler>();

            services.AddScoped<
                IIntegrationEventHandler<LotDeletedIntegrationEvent>,
                DeleteLotRealTimeHandler>();


            return services;
        }
    }
}
