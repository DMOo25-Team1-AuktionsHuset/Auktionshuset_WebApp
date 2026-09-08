using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Auktionshuset.Contracts.Dto.Admin.Lot;

namespace Auktionshuset.Services;

public sealed class LotService(HttpClient httpClient)
{
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

    private static async Task<string> ReadProblemMessageAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
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
            : $"Lot kunne ikke oprettes (serverfejl {(int)response.StatusCode}).";
    }
}

public sealed class LotApiException(
    string message,
    HttpStatusCode statusCode,
    Exception? innerException = null) : Exception(message, innerException)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
}
