using System.Net;
using System.Text.Json;

namespace Auktionshuset.Services;

/// <summary>
/// Reads the user-facing message out of a failed API response, so every client service reports
/// server errors in the same way.
/// </summary>
internal static class ApiProblemReader
{
    /// <summary>
    /// Extracts a Danish message from a failed response, falling back to a status-based message
    /// built from the supplied subject and verb.
    /// </summary>
    /// <param name="response">The failed response to read the message from.</param>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <param name="subject">The Danish noun inserted into the fallback message, for example "Genstanden".</param>
    /// <param name="verb">The Danish verb inserted into the fallback message, for example "slettes".</param>
    /// <returns>The message to display to the user.</returns>
    public static async Task<string> ReadMessageAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken,
        string subject,
        string verb)
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
            // Fall through to a status-based message for non-JSON responses.
        }
        catch (NotSupportedException)
        {
            // The response did not carry JSON at all.
        }

        return response.StatusCode == HttpStatusCode.BadRequest
            ? "Oplysningerne blev afvist. Kontrollér felterne og prøv igen."
            : $"{subject} kunne ikke {verb} (serverfejl {(int)response.StatusCode}).";
    }
}

/// <summary>
/// Represents an error returned by the API, exposing the HTTP status code that caused it.
/// </summary>
public class ApiException(
    string message,
    HttpStatusCode statusCode,
    Exception? innerException = null) : Exception(message, innerException)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
}

public sealed class LotApiException(
    string message,
    HttpStatusCode statusCode,
    Exception? innerException = null) : ApiException(message, statusCode, innerException);

public sealed class AuctionApiException(
    string message,
    HttpStatusCode statusCode,
    Exception? innerException = null) : ApiException(message, statusCode, innerException);

public sealed class EmployeeApiException(
    string message,
    HttpStatusCode statusCode,
    Exception? innerException = null) : ApiException(message, statusCode, innerException);
