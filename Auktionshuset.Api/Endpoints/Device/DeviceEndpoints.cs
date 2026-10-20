namespace Auktionshuset.Api.Endpoints.Device
{
    public static class DeviceEndpoints
    {
        public static IEndpointRouteBuilder MapDeviceTestEndpoints(
            this IEndpointRouteBuilder endpoints)
        {
            endpoints.MapDeviceConnectionTest();

            return endpoints;
        }
    }
}
