using Auktionshuset.Api.Endpoints.Admin.UpdateLot;
using Auktionshuset.Api.Endpoints.Admin.DeleteLot;
using Auktionshuset.Api.Endpoints.Admin.GetLots;

namespace Auktionshuset.Api.Endpoints.Admin.CreateLot {
    public static class LotEndpoints {
        /// <summary>
        /// Maps every lot endpoint under the <c>/api/lots</c> route group.
        /// </summary>
        /// <param name="endpoints">The endpoint route builder that the route group is added to.</param>
        /// <returns>The same endpoint route builder so that further routes can be mapped.</returns>
        public static IEndpointRouteBuilder MapLotEndpoints(this IEndpointRouteBuilder endpoints) {
            var group = endpoints
                .MapGroup("/api/lots")
                .WithTags("Lots");

            group.MapCreateLot();
            group.MapUpdateLot();
            group.MapGetLots();
            group.MapDeleteLot();

            return endpoints;
        }
    }
}

