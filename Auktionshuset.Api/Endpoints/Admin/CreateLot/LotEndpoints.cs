using Auktionshuset.Api.Endpoints.Admin.GetLots;

namespace Auktionshuset.Api.Endpoints.Admin.CreateLot {
    public static class LotEndpoints {
        public static IEndpointRouteBuilder MapLotEndpoints(this IEndpointRouteBuilder endpoints) {
            var group = endpoints
                .MapGroup("/api/lots")
                .WithTags("Lots");

            group.MapCreateLot();
            group.MapGetLots();

            return endpoints;
        }
    }
}
