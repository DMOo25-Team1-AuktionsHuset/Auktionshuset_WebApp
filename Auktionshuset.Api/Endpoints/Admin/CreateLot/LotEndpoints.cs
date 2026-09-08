namespace Auktionshuset.Api.Endpoints.Admin.CreateLot {
    public static class LotEndpoints {
        public static IEndpointRouteBuilder MapLotEndpoints(this IEndpointRouteBuilder endpoints) {
            var group = endpoints
                .MapGroup("/api/lots")
                .WithTags("Lots");

            group.MapCreateLot();

            return endpoints;
        }
    }
}
