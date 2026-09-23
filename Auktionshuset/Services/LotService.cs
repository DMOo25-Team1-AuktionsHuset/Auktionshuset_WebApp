using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Auktionshuset.Contracts.Dto.Admin.Lot;
using Auktionshuset.Contracts.Dto.Admin.Lot.Image;
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
            var message = await ApiProblemReader.ReadMessageAsync(response, cancellationToken, "Genstandene", "hentes");
            throw new LotApiException(message, response.StatusCode);
        }

        try
        {
            return await response.Content.ReadFromJsonAsync<IReadOnlyList<LotListItemResponse>>(cancellationToken)
                ?? [];
        }
        catch (JsonException exception)
        {
            throw new LotApiException("Serveren returnerede ikke en gyldig liste over genstande.", response.StatusCode, exception);
        }
    }

    /// <summary>
    /// Creates a lot through the API.
    /// </summary>
    /// <param name="request">The lot values to send to the API.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
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
            return await ReadAsync<CreateLotResponse>(
                response,
                "Serveren returnerede ikke et gyldigt id for genstanden.",
                cancellationToken);
        }

        var message = await ApiProblemReader.ReadMessageAsync(response, cancellationToken, "Genstanden", "oprettes");
        throw new LotApiException(message, response.StatusCode);
    }

    /// <summary>
    /// Updates the lot with the given identifier through the API.
    /// </summary>
    /// <param name="lotId">The identifier of the lot to update.</param>
    /// <param name="request">The replacement lot values to send to the API.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The identifier of the updated lot.</returns>
    /// <exception cref="LotApiException">
    /// Thrown when the lot no longer exists, when the API rejects the request, or when the response
    /// body is not valid JSON.
    /// </exception>
    public async Task<UpdateLotResponse> UpdateAsync(
        Guid lotId,
        UpdateLotRequest request,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PutAsJsonAsync($"api/lots/{lotId}", request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new LotApiException(
                "Genstanden findes ikke længere. Listen kan være ændret af en anden bruger.",
                response.StatusCode);
        }

        if (response.IsSuccessStatusCode)
        {
            return await ReadAsync<UpdateLotResponse>(
                response,
                "Serveren returnerede ikke et gyldigt id for genstanden.",
                cancellationToken);
        }

        var message = await ApiProblemReader.ReadMessageAsync(response, cancellationToken, "Genstanden", "opdateres");
        throw new LotApiException(message, response.StatusCode);
    }

    /// <summary>
    /// Deletes the lot with the given identifier through the API.
    /// </summary>
    /// <param name="lotId">The identifier of the lot to delete.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A task that completes when the API has processed the deletion.</returns>
    /// <exception cref="LotApiException">
    /// Thrown when the lot no longer exists or the API call otherwise fails.
    /// </exception>
    public async Task DeleteAsync(
        Guid lotId,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.DeleteAsync($"api/lots/{lotId}", cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return;
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new LotApiException("Genstanden blev ikke fundet.", response.StatusCode);
        }

        var message = await ApiProblemReader.ReadMessageAsync(response, cancellationToken, "Genstanden", "slettes");
        throw new LotApiException(message, response.StatusCode);
    }

    /// <summary>
    /// Attaches an image to a lot, replacing any image it already had.
    /// </summary>
    /// <param name="lotId">The identifier of the lot the image belongs to.</param>
    /// <param name="content">The image bytes to upload. The stream is disposed together with the request.</param>
    /// <param name="fileName">The file name reported to the server, used only for diagnostics.</param>
    /// <param name="contentType">The content type reported to the server.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>The stored image reference.</returns>
    /// <exception cref="LotApiException">Thrown when the image is rejected or the lot does not exist.</exception>
    public async Task<LotImageResponse> UploadImageAsync(
        Guid lotId,
        Stream content,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        using MultipartFormDataContent form = new MultipartFormDataContent();
        StreamContent fileContent = new StreamContent(content);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(
            string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType);
        form.Add(fileContent, "file", string.IsNullOrWhiteSpace(fileName) ? "billede" : fileName);

        using var response = await httpClient.PostAsync($"api/lots/{lotId}/image", form, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return await ReadAsync<LotImageResponse>(
                response,
                "Serveren returnerede ikke en gyldig billedreference.",
                cancellationToken);
        }

        var message = await ApiProblemReader.ReadMessageAsync(response, cancellationToken, "Billedet", "gemmes");
        throw new LotApiException(message, response.StatusCode);
    }

    /// <summary>
    /// Removes the image of a lot.
    /// </summary>
    /// <param name="lotId">The identifier of the lot whose image is removed.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A task that completes when the API has removed the image.</returns>
    /// <exception cref="LotApiException">Thrown when the lot does not exist or the call fails.</exception>
    public async Task RemoveImageAsync(Guid lotId, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.DeleteAsync($"api/lots/{lotId}/image", cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var message = await ApiProblemReader.ReadMessageAsync(response, cancellationToken, "Billedet", "fjernes");
        throw new LotApiException(message, response.StatusCode);
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
                ?? throw new LotApiException(errorMessage, response.StatusCode);
        }
        catch (JsonException exception)
        {
            throw new LotApiException(errorMessage, response.StatusCode, exception);
        }
    }
}
