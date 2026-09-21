using Auktionshuset.Api.Endpoints.Admin.Lot.DeleteLot;
using Auktionshuset.Api.Endpoints.Admin.Lot.GetLots;
using Auktionshuset.Api.Endpoints.Admin.Lot.UpdateLot;
using Auktionshuset.Api.Endpoints.Admin.LotImage;
using Auktionshuset.Api.Endpoints.Admin.Lots.CreateLot;

namespace Auktionshuset.Api.Endpoints.Admin.Lots {
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
            group.MapLotImageEndpoints();

            return endpoints;
        }
    }
}
