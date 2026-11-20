using System.Net;
using System.Text.Json;
using Auktionshuset.Contracts.Dto.Customer.Request;
using Auktionshuset.Contracts.Dto.Customer.Response;

namespace Auktionshuset.Services;

public sealed class CustomerService(BackendApiClient httpClient)
{
    public async Task<CustomerProfileResponse> GetOwnProfileAsync(
        CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await httpClient.GetAsync("api/customer/me", cancellationToken);
        await EnsureSuccessAsync(response, "Dine oplysninger", "hentes", cancellationToken);

        try
        {
            return await response.Content.ReadFromJsonAsync<CustomerProfileResponse>(cancellationToken)
                ?? throw new JsonException("Tomt svar");
        }
        catch (JsonException exception)
        {
            throw new CustomerApiException("Serveren returnerede ikke gyldige kundeoplysninger.", response.StatusCode, exception);
        }
    }

    public async Task<CustomerProfileResponse> UpdateOwnProfileAsync(
        UpdateCustomerProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await httpClient.PutAsJsonAsync("api/customer/me", request, cancellationToken);
        await EnsureSuccessAsync(response, "Dine oplysninger", "gemmes", cancellationToken);

        try
        {
            return await response.Content.ReadFromJsonAsync<CustomerProfileResponse>(cancellationToken)
                ?? throw new JsonException("Tomt svar");
        }
        catch (JsonException exception)
        {
            throw new CustomerApiException("Serveren returnerede ikke et gyldigt svar på gemningen.", response.StatusCode, exception);
        }
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        string subject,
        string verb,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        string message = response.StatusCode == HttpStatusCode.Forbidden
            ? "Du har ikke adgang til denne side. Kontakt auktionshuset, hvis problemet fortsætter."
            : await ApiProblemReader.ReadMessageAsync(response, subject, verb, cancellationToken);

        throw new CustomerApiException(message, response.StatusCode);
    }
}

public sealed class CustomerApiException(
    string message,
    HttpStatusCode statusCode,
    Exception? innerException = null) : ApiException(message, statusCode, innerException);

