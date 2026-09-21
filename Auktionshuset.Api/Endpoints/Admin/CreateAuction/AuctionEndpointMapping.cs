using Auktionshuset.Application.Admin.Auctions;
using Auktionshuset.Contracts.Dto.Admin.Auction;

namespace Auktionshuset.Api.Endpoints.Admin.CreateAuction
{
    /// <summary>
    /// Maps the auction contracts onto the application commands and the validation problems that
    /// the endpoints return.
    /// </summary>
    internal static class AuctionEndpointMapping
    {
        /// <summary>
        /// Projects the requested lot lines into the selection type used by the handlers.
        /// </summary>
        /// <param name="lots">The requested lot lines, possibly <see langword="null"/>.</param>
        /// <returns>The selections to validate and store.</returns>
        internal static AuctionLotSelection[] ToSelections(AuctionLotRequest[]? lots) =>
            lots is null
                ? []
                : lots
                    .Select(lot => new AuctionLotSelection(lot.LotId, lot.Quantity))
                    .ToArray();

        /// <summary>
        /// Converts handler error messages into the keyed dictionary that a validation problem uses.
        /// </summary>
        /// <param name="errors">The error messages collected by the handler.</param>
        /// <returns>A dictionary keyed by request position.</returns>
        internal static Dictionary<string, string[]> ToValidationErrors(IReadOnlyCollection<string> errors) =>
            errors
                .Select((message, index) => (Key: $"request[{index}]", Messages: new[] { message }))
                .ToDictionary(error => error.Key, error => error.Messages);
    }
}
