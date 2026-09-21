using System.Net.Http.Json;
using System.Text.Json;
using Auktionshuset.Contracts.Dto.Admin.Employee;

namespace Auktionshuset.Services;

public sealed class EmployeeService(HttpClient httpClient)
{
    /// <summary>
    /// Fetches every employee that can be chosen as auctionarius.
    /// </summary>
    /// <param name="cancellationToken">The token used to cancel the operation.</param>
    /// <returns>A read-only list containing every employee returned by the API.</returns>
    /// <exception cref="EmployeeApiException">
    /// Thrown when the API responds with an error status, or when the response body is not valid JSON.
    /// </exception>
    public async Task<IReadOnlyList<EmployeeListItemResponse>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync("api/employees", cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var message = await ApiProblemReader.ReadMessageAsync(
                response,
                cancellationToken,
                "Medarbejderne",
                "hentes");

            throw new EmployeeApiException(message, response.StatusCode);
        }

        try
        {
            return await response.Content.ReadFromJsonAsync<IReadOnlyList<EmployeeListItemResponse>>(cancellationToken)
                ?? [];
        }
        catch (JsonException exception)
        {
            throw new EmployeeApiException(
                "Serveren returnerede ikke en gyldig medarbejderliste.",
                response.StatusCode,
                exception);
        }
    }
}
