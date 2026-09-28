using System.Net;
using System.Text.Json;
using Auktionshuset.Contracts.Dto.Admin.Employee;
using Auktionshuset.Contracts.Dto.Admin.Employee.CreateEmployee;

using Auktionshuset.Contracts.Dto.Admin.Employee.UpdateEmployee;

namespace Auktionshuset.Services;

public sealed class EmployeeService(HttpClient httpClient)
{
    public async Task<IReadOnlyList<EmployeeListItemResponse>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await httpClient.GetAsync("api/employee", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken, "Medarbejderne", "hentes");

        try
        {
            return await response.Content.ReadFromJsonAsync<IReadOnlyList<EmployeeListItemResponse>>(cancellationToken)
                ?? [];
        }
        catch (JsonException exception)
        {
            throw new EmployeeApiException("Serveren returnerede ikke en gyldig medarbejderliste.", response.StatusCode, exception);
        }
    }

    public async Task<EmployeeResponse> GetByIdAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await httpClient.GetAsync($"api/employee/{employeeId}", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken, "Medarbejderen", "hentes");

        try
        {
            return await response.Content.ReadFromJsonAsync<EmployeeResponse>(cancellationToken)
                ?? throw new JsonException("Tomt svar");
        }
        catch (JsonException exception)
        {
            throw new EmployeeApiException("Serveren returnerede ikke gyldige medarbejderoplysninger.", response.StatusCode, exception);
        }
    }

    public async Task<CreateEmployeeResponse> CreateAsync(
        CreateEmployeeRequest request,
        CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await httpClient.PostAsJsonAsync("api/employee", request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken, "Medarbejderen", "oprettes");

        try
        {
            return await response.Content.ReadFromJsonAsync<CreateEmployeeResponse>(cancellationToken)
                ?? throw new JsonException("Tomt svar");
        }
        catch (JsonException exception)
        {
            throw new EmployeeApiException("Serveren returnerede ikke et gyldigt svar på oprettelsen.", response.StatusCode, exception);
        }
    }

    public async Task<UpdateEmployeeResponse> UpdateAsync(
        Guid employeeId,
        UpdateEmployeeRequest request,
        CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await httpClient.PutAsJsonAsync($"api/employee/{employeeId}", request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken, "Medarbejderen", "opdateres");

        try
        {
            return await response.Content.ReadFromJsonAsync<UpdateEmployeeResponse>(cancellationToken)
                ?? throw new JsonException("Tomt svar");
        }
        catch (JsonException exception)
        {
            throw new EmployeeApiException("Serveren returnerede ikke et gyldigt svar på opdateringen.", response.StatusCode, exception);
        }
    }

    public async Task DeleteAsync(Guid employeeId, CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await httpClient.DeleteAsync($"api/employee/{employeeId}", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken, "Medarbejderen", "slettes");
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken,
        string subject,
        string verb)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        string message = response.StatusCode == HttpStatusCode.NotFound
            ? "Medarbejderen findes ikke længere. Listen er muligvis blevet opdateret."
            : await ApiProblemReader.ReadMessageAsync(response, cancellationToken, subject, verb);

        throw new EmployeeApiException(message, response.StatusCode);
    }
}
