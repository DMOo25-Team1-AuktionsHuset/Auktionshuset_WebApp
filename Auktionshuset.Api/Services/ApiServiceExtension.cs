using Auktionshuset.Api.Services.DeviceGateway;
using Auktionshuset.Application.Admin.Devices.RegisterDevice;
using Auktionshuset.Application.Admin.Devices;
using Auktionshuset.Application.Devices.AuthenticateDevice;
using Auktionshuset.Api.Events.Admin.Auction;
using Auktionshuset.Api.Events.Admin;
using Auktionshuset.Application.Abstraction.Auction;
using Auktionshuset.Application.Admin.Auctions.Bids;
using Auktionshuset.Application.Admin.Auctions;
using Auktionshuset.Application.EventHandling;

namespace Auktionshuset.Api.Services
{
    public static class ApiServiceExtension
    {
        /// <summary>
        /// Registers the services owned by the API layer.
        /// </summary>
        /// <param name="services">The service collection to add the API services to.</param>
        /// <returns>The same service collection so that further calls can be chained.</returns>
        public static IServiceCollection AddApiServices(
            this IServiceCollection services)
        {
            services.AddSingleton<DeviceGatewayProcessor>();
            services.AddSingleton<IDeviceCurrentStateReader, DeviceCurrentStateReader>();
            services.AddHostedService<DeviceGatewayHubService>();
            services.AddLotHandler();
            services.AddEmployeeHandler();
            services.AddAuctionHandlers();
            services.AddScoped<PlaceBidHandler>();
            services.AddScoped<CustomerProfileService>();
            services.AddScoped<CustomerRegistrationService>();
            services.AddScoped<IDeviceGatewayInvalidations, DeviceGatewayHubInvalidations>();
            services.AddScoped<IIntegrationEventHandler<AuctionRevisionChangedIntegrationEvent>, DeviceGatewayInvalidationHandler>();
            services.AddScoped<IIntegrationEventHandler<DeviceIssuedIntegrationEvent>, DeviceGatewayInvalidationHandler>();
            services.AddScoped<IIntegrationEventHandler<DeviceReleasedIntegrationEvent>, DeviceGatewayInvalidationHandler>();
            services.AddScoped<IIntegrationEventHandler<AuctionRevisionChangedIntegrationEvent>, AuctionRevisionChangedRealTimeHandler>();
            services.AddScoped<IIntegrationEventHandler<BidPlacedIntegrationEvent>,
                BidPlacedRealTimeHandler>();

            services.AddScoped<IIntegrationEventHandler<DeviceRegisteredIntegrationEvent>, DeviceEventAuditHandler>();
            services.AddScoped<IIntegrationEventHandler<DeviceIssuedIntegrationEvent>, DeviceEventAuditHandler>();
            services.AddScoped<IIntegrationEventHandler<DeviceReleasedIntegrationEvent>, DeviceEventAuditHandler>();
            services.AddScoped<RegisterDeviceHandler>();
            services.AddScoped<AuthenticateDeviceHandler>();
            services.AddScoped<IssueDeviceHandler>();
            services.AddScoped<ReleaseDeviceHandler>();
            services.AddScoped<DeactivateDeviceHandler>();
            services.AddScoped<ValidateDeviceSessionHandler>();
            services.AddScoped<PlaceDeviceBidHandler>();
            return services;
        }
    }
}
