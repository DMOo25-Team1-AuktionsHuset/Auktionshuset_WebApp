using Auktionshuset.Application.Abstraction.Auction;

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
            services.AddLotHandler();
            services.AddEmployeeHandler();
            services.AddAuctionHandlers();
            //services.AddScoped<PlaceBidHandler>();

            return services;
        }
    }
}
