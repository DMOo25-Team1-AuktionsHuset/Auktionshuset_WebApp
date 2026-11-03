namespace Auktionshuset.Api.Endpoints.Device;

public static class DeviceEndpoints
{
    public static IEndpointRouteBuilder MapDeviceEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapDeviceAuthentication();

        return endpoints;
    }

    public static IEndpointRouteBuilder MapDeviceTestEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapDeviceConnectionTest();

        return endpoints;
    }
}
