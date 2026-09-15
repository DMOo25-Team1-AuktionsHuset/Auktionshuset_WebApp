namespace Auktionshuset.Api.Services
{
    public static class ApiServiceExtension
    {
        public static IServiceCollection AddApiServices(
            this IServiceCollection services)
        {
            // Add API services here
            services.AddLotHandler();

            return services;
        }
    }
}
