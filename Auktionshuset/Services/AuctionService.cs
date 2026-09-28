using System.Net;
using System.Text.Json;
using Auktionshuset.Contracts.Dto.Admin.Auction;

namespace Auktionshuset.Services;

public sealed class AuctionService(HttpClient httpClient)
{
    /// <summary>
    /// Fetches every auction for the dashboard.
    /// </summary>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A read-only list containing every auction returned by the API.</returns>
    /// <exception cref="AuctionApiException">
    /// Thrown when the API responds with an error status, or when the response body is not valid JSON.
    /// </exception>
    public async Task<IReadOnlyList<AuctionListItemResponse>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await httpClient.GetAsync("api/auctions", cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            string message = await ApiProblemReader.ReadMessageAsync(
                response,
                cancellationToken,
                "Auktionerne",
                "hentes");

            throw new AuctionApiException(message, response.StatusCode);
        }

        try
        {
            return await response.Content.ReadFromJsonAsync<IReadOnlyList<AuctionListItemResponse>>(cancellationToken)
                ?? [];
        }
        catch (JsonException exception)
        {
            throw new AuctionApiException(
                "Serveren returnerede ikke en gyldig auktionsliste.",
                response.StatusCode,
                exception);
        }
    }

    /// <summary>
    /// Fetches a single auction with its lot lines.
    /// </summary>
    /// <param name="auctionId">The identifier of the auction to load.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The auction, or <see langword="null"/> when it no longer exists.</returns>
    /// <exception cref="AuctionApiException">Thrown when the API call fails.</exception>
    public async Task<AuctionDetailResponse?> GetByIdAsync(
        Guid auctionId,
        CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await httpClient.GetAsync($"api/auctions/{auctionId}", cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            string message = await ApiProblemReader.ReadMessageAsync(
                response,
                cancellationToken,
                "Auktionen",
                "hentes");

            throw new AuctionApiException(message, response.StatusCode);
        }

        try
        {
            return await response.Content.ReadFromJsonAsync<AuctionDetailResponse>(cancellationToken);
        }
        catch (JsonException exception)
        {
            throw new AuctionApiException(
                "Serveren returnerede ikke en gyldig auktion.",
                response.StatusCode,
                exception);
        }
    }

    /// <summary>
    /// Creates an auction through the API.
    /// </summary>
    /// <param name="request">The auction values to send to the API.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The identifier of the new auction and its totals.</returns>
    /// <exception cref="AuctionApiException">Thrown when the API rejects the request.</exception>
    public async Task<CreateAuctionResponse> CreateAsync(
        CreateAuctionRequest request,
        CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await httpClient.PostAsJsonAsync("api/auctions", request, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return await ReadAsync<CreateAuctionResponse>(
                response,
                "Serveren returnerede ikke et gyldigt auktions-id.",
                cancellationToken);
        }

        string message = await ApiProblemReader.ReadMessageAsync(response, cancellationToken, "Auktionen", "oprettes");
        throw new AuctionApiException(message, response.StatusCode);
    }

    /// <summary>
    /// Updates an auction through the API.
    /// </summary>
    /// <param name="auctionId">The identifier of the auction to update.</param>
    /// <param name="request">The replacement auction values to send to the API.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The identifier of the updated auction and its totals.</returns>
    /// <exception cref="AuctionApiException">Thrown when the API rejects the request or the auction is gone.</exception>
    public async Task<UpdateAuctionResponse> UpdateAsync(
        Guid auctionId,
        UpdateAuctionRequest request,
        CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await httpClient.PutAsJsonAsync($"api/auctions/{auctionId}", request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new AuctionApiException(
                "Auktionen findes ikke længere. Listen kan være ændret af en anden bruger.",
                response.StatusCode);
        }

        if (response.IsSuccessStatusCode)
        {
            return await ReadAsync<UpdateAuctionResponse>(
                response,
                "Serveren returnerede ikke et gyldigt auktions-id.",
                cancellationToken);
        }

        string message = await ApiProblemReader.ReadMessageAsync(response, cancellationToken, "Auktionen", "gemmes");
        throw new AuctionApiException(message, response.StatusCode);
    }

    /// <summary>
    /// Deletes an auction through the API.
    /// </summary>
    /// <param name="auctionId">The identifier of the auction to delete.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A task that completes when the API has processed the deletion.</returns>
    /// <exception cref="AuctionApiException">Thrown when the auction no longer exists or the call fails.</exception>
    public async Task DeleteAsync(Guid auctionId, CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await httpClient.DeleteAsync($"api/auctions/{auctionId}", cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return;
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new AuctionApiException("Auktionen blev ikke fundet.", response.StatusCode);
        }

        string message = await ApiProblemReader.ReadMessageAsync(response, cancellationToken, "Auktionen", "slettes");
        throw new AuctionApiException(message, response.StatusCode);
    }

    /// <summary>
    /// Turns the relative image URL returned by the API into a URL the browser can load.
    /// </summary>
    /// <param name="relativeUrl">The relative URL from the API, or <see langword="null"/>.</param>
    /// <returns>The absolute URL, or <see langword="null"/> when no image is attached.</returns>
    public string? ResolveImageUrl(string? relativeUrl)
    {
        if (string.IsNullOrWhiteSpace(relativeUrl))
        {
            return null;
        }

        return httpClient.BaseAddress is null
            ? relativeUrl
            : new Uri(httpClient.BaseAddress, relativeUrl).ToString();
    }

    private static async Task<T> ReadAsync<T>(
        HttpResponseMessage response,
        string errorMessage,
        CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<T>(cancellationToken)
                ?? throw new AuctionApiException(errorMessage, response.StatusCode);
        }
        catch (JsonException exception)
        {
            throw new AuctionApiException(errorMessage, response.StatusCode, exception);
        }
    }
}
