using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Auktionshuset.Contracts.Dto.Admin.Lot;
using Auktionshuset.Contracts.Dto.Admin.Lot.UpdateLot;
using Auktionshuset.Contracts.Dto.Admin.Lot.CreateLot;

namespace Auktionshuset.Services;

public sealed class LotService(HttpClient httpClient)
{

    /// <summary>
    /// Fetches every lot from the API.
    /// </summary>
    /// <returns>A read-only list containing every lot returned by the API.</returns>
    /// <exception cref="LotApiException">
    /// Thrown when the API responds with an error status, or when the response body is not valid JSON.
    /// </exception>
    public async Task<IReadOnlyList<LotListItemResponse>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync("api/lots", cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var message = await ReadProblemMessageAsync(response, cancellationToken, "hentes");
            throw new LotApiException(message, response.StatusCode);
        }

        try
        {
            return await response.Content.ReadFromJsonAsync<IReadOnlyList<LotListItemResponse>>(cancellationToken)
                ?? [];
        }
        catch (JsonException exception)
        {
            throw new LotApiException("Serveren returnerede ikke en gyldig lotliste.", response.StatusCode, exception);
        }
    }

    /// <summary>
    /// Creates a lot through the API.
    /// </summary>
    /// <param name="request">The lot values to send to the API.</param>
    /// <returns>The identifier assigned to the new lot.</returns>
    /// <exception cref="LotApiException">
    /// Thrown when the API rejects the request, or when the response body does not contain a valid
    /// lot identifier.
    /// </exception>
    public async Task<CreateLotResponse> CreateAsync(
        CreateLotRequest request,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync("api/lots", request, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            try
            {
                return await response.Content.ReadFromJsonAsync<CreateLotResponse>(cancellationToken)
                    ?? throw new LotApiException("Serveren returnerede ikke et gyldigt lot-id.", response.StatusCode);
            }
            catch (JsonException exception)
            {
                throw new LotApiException("Serveren returnerede ikke et gyldigt lot-id.", response.StatusCode, exception);
            }
        }

        var message = await ReadProblemMessageAsync(response, cancellationToken);
        throw new LotApiException(message, response.StatusCode);
    }

    /// <summary>
    /// Updates the lot with the given identifier through the API.
    /// </summary>
    /// <param name="lotId">The identifier of the lot to update.</param>
    /// <param name="request">The replacement lot values to send to the API.</param>
    /// <returns>The identifier of the updated lot.</returns>
    /// <exception cref="LotApiException">
    /// Thrown when the lot no longer exists, when the API rejects the request, or when the response
    /// body is not valid JSON.
    /// </exception>
    public async Task<UpdateLotResponse> UpdateAsync(Guid lotId, UpdateLotRequest request, CancellationToken cancellationToken = default) {
        using var response = await httpClient.PutAsJsonAsync($"api/lots/{lotId}", request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound) {
            throw new LotApiException("Lot not found, the list could have been changed", response.StatusCode);
        }

        if (response.IsSuccessStatusCode) {
            try {
                return await response.Content
                    .ReadFromJsonAsync<UpdateLotResponse>(cancellationToken)
                    ?? throw new LotApiException("The server did not return a valid lot id", response.StatusCode);
            } catch (JsonException ex) {
                throw new LotApiException("The server did not return a valid lot id", response.StatusCode, ex);
            }
        }

        var message = await ReadProblemMessageAsync(response, cancellationToken, "updating");

        throw new LotApiException(message, response.StatusCode);
    }

    /// <summary>
    /// Deletes the lot with the given identifier through the API.
    /// </summary>
    /// <param name="lotId">The identifier of the lot to delete.</param>
    /// <returns>A task that completes when the API has processed the deletion.</returns>
    /// <exception cref="LotApiException">
    /// Thrown when the lot no longer exists or the API call otherwise fails.
    /// </exception>
    public async Task DeleteAsync(
        Guid lotId, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.DeleteAsync($"api/lots/{lotId}", cancellationToken);
        
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new LotApiException("Lot blev ikke fundet.", response.StatusCode);
        }

        var message = await ReadProblemMessageAsync(response, cancellationToken, "slettes");
        throw new LotApiException(message, response.StatusCode);
    }

    /// <summary>
    /// Extracts a user-facing error message from a failed API response, falling back to a generic
    /// message derived from the status code.
    /// </summary>
    /// <param name="response">The failed response to read the message from.</param>
    /// <param name="action">The Danish verb inserted into the fallback message, for example "oprettes".</param>
    /// <returns>The message to display to the user.</returns>
    private static async Task<string> ReadProblemMessageAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken,
        string action = "oprettes")
    {
        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var root = document.RootElement;

            if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
            {
                var validationMessages = errors.EnumerateObject()
                    .SelectMany(error => error.Value.ValueKind == JsonValueKind.Array
                        ? error.Value.EnumerateArray()
                            .Select(value => value.GetString())
                            .Where(value => !string.IsNullOrWhiteSpace(value))
                        : [])
                    .Distinct()
                    .ToArray();

                if (validationMessages.Length > 0)
                {
                    return $"Oplysningerne blev afvist: {string.Join(" ", validationMessages)}";
                }
            }

            if (root.TryGetProperty("detail", out var detail)
                && !string.IsNullOrWhiteSpace(detail.GetString()))
            {
                return detail.GetString()!;
            }

            if (root.TryGetProperty("title", out var title)
                && !string.IsNullOrWhiteSpace(title.GetString()))
            {
                return title.GetString()!;
            }
        }
        catch (JsonException)
        {
            // Fall through to a useful status-based message for non-JSON responses.
        }

        return response.StatusCode == HttpStatusCode.BadRequest
            ? "Oplysningerne blev afvist. Kontrollér felterne og prøv igen."
            : $"Lot kunne ikke {action} (serverfejl {(int)response.StatusCode}).";
    }
}

/// <summary>
/// Represents an error returned by the lot API, exposing the HTTP status code that caused it.
/// </summary>
public sealed class LotApiException(
    string message,
    HttpStatusCode statusCode,
    Exception? innerException = null) : Exception(message, innerException)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
}

