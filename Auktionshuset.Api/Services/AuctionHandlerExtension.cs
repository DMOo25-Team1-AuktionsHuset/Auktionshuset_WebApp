using Auktionshuset.Api.Events.Admin.Auction;
using Auktionshuset.Application.Admin.Auctions.CreateAuction;
using Auktionshuset.Application.Admin.Auctions.DeleteAuction;
using Auktionshuset.Application.Admin.Auctions.GetAuctions;
using Auktionshuset.Application.Admin.Auctions.UpdateAuction;
using Auktionshuset.Application.Admin.Employees;
using Auktionshuset.Application.EventHandling;

namespace Auktionshuset.Api.Services
{
    internal static class AuctionHandlerExtension
    {
        /// <summary>
        /// Registers the auction and employee handlers together with the real-time handlers that
        /// react to the auction integration events.
        /// </summary>
        /// <param name="services">The service collection to add the handlers to.</param>
        /// <returns>The same service collection so that further calls can be chained.</returns>
        internal static IServiceCollection AddAuctionHandlers(
            this IServiceCollection services)
        {
            services.AddScoped<CreateAuctionHandler>();
            services.AddScoped<UpdateAuctionHandler>();
            services.AddScoped<DeleteAuctionHandler>();
            services.AddScoped<GetAuctionsHandler>();
            services.AddScoped<GetEmployeesHandler>();

            services.AddScoped<
                IIntegrationEventHandler<AuctionCreatedIntegrationEvent>,
                CreateAuctionRealTimeHandler>();

            services.AddScoped<
                IIntegrationEventHandler<AuctionUpdatedIntegrationEvent>,
                UpdateAuctionRealTimeHandler>();

            services.AddScoped<
                IIntegrationEventHandler<AuctionDeletedIntegrationEvent>,
                DeleteAuctionRealTimeHandler>();

            return services;
        }
    }
}
