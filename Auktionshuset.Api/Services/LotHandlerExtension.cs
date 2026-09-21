using Auktionshuset.Api.Events.Admin.Lot;
using Auktionshuset.Application.Admin.Lots;
using Auktionshuset.Application.Admin.Lots.CreateLot;
using Auktionshuset.Application.Admin.Lots.DeleteLot;
using Auktionshuset.Application.Admin.Lots.Images;
using Auktionshuset.Application.Admin.Lots.UpdateLot;
using Auktionshuset.Application.EventHandling;

namespace Auktionshuset.Api.Services
{
    internal static class LotHandlerExtension
    {
        /// <summary>
        /// Registers the lot command handlers together with the real-time handlers that react to
        /// their integration events.
        /// </summary>
        /// <param name="services">The service collection to add the lot handlers to.</param>
        /// <returns>The same service collection so that further calls can be chained.</returns>
        internal static IServiceCollection AddLotHandler(
            this IServiceCollection services)
        {
            services.AddScoped<CreateLotHandler>();
            services.AddScoped<UpdateLotHandler>();
            services.AddScoped<GetLotsHandler>();
            services.AddScoped<DeleteLotHandler>();
            services.AddScoped<UploadLotImageHandler>();
            services.AddScoped<RemoveLotImageHandler>();

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
