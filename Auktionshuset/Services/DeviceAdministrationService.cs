using System.Text.Json;

namespace Auktionshuset.Services;

public sealed class DeviceAdministrationService(BackendApiClient client)
{
    public async Task<DeviceOverview> GetOverviewAsync()
    {
        var customers = GetAsync<List<DeviceCustomer>>("api/staff/devices/customers");
        var auctions = GetAsync<List<DeviceAuction>>("api/staff/devices/auctions");
        var available = GetAsync<List<AvailableDevice>>("api/staff/devices/available");
        var sessions = GetAsync<List<DeviceSessionItem>>("api/staff/devices/sessions");
        await Task.WhenAll(customers, auctions, available, sessions);
        return new DeviceOverview(await customers, await auctions, await available, await sessions);
    }

    public async Task<DeviceRegistration> RegisterAsync(Guid auctionHouseId, int number)
    {
        using var response = await client.PostAsJsonAsync("api/staff/devices/", new { auctionHouseId, deviceNumber = number });
        await CheckAsync(response);
        var result = await response.Content.ReadFromJsonAsync<DeviceRegistration>();
        if (result is null || result.DeviceId == Guid.Empty || string.IsNullOrWhiteSpace(result.Secret))
            throw new HttpRequestException("Registreringen kan være gennemført, men hemmeligheden kunne ikke læses. Registrér ikke igen uden at undersøge resultatet.");
        return result;
    }

    public Task IssueAsync(Guid deviceId, Guid auctionId, Guid customerId) =>
        PostAsync($"api/staff/devices/{deviceId}/issue", new { auctionId, customerId });
    public Task ReleaseAsync(Guid deviceId, Guid auctionId, Guid sessionId) =>
        PostAsync($"api/staff/devices/{deviceId}/release", new { auctionId, sessionId });

    private async Task<T> GetAsync<T>(string path)
    {
        using var response = await client.GetAsync(path);
        await CheckAsync(response);
        return await response.Content.ReadFromJsonAsync<T>() ?? throw new HttpRequestException("Serveren returnerede et tomt svar.");
    }

    private async Task PostAsync<T>(string path, T value)
    {
        using var response = await client.PostAsJsonAsync(path, value);
        await CheckAsync(response);
    }

    private static async Task CheckAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;
        string? message = null;
        try
        {
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = json.RootElement;
            if (root.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.String)
                message = error.GetString() switch
                {
                    "DeviceAlreadyAssigned" => "Devicet er allerede udleveret.",
                    "CustomerAlreadyAssigned" => "Kunden har allerede et device til auktionen.",
                    "AuctionHouseMismatch" => "Devicet tilhører et andet auktionshus.",
                    "DeviceDisabled" => "Devicet er deaktiveret.",
                    "SessionNotFound" => "Sessionen findes ikke længere.",
                    "DeviceNotFound" => "Devicet findes ikke længere.",
                    "AuctionNotFound" => "Auktionen findes ikke længere.",
                    "CustomerNotFound" => "Kunden findes ikke længere.",
                    "InvalidRequest" => "Oplysningerne blev afvist.",
                    _ => null
                };
            if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array)
                message = string.Join(" ", errors.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.String).Select(e => e.GetString()));
        }
        catch (JsonException) { }
        if (string.IsNullOrWhiteSpace(message))
            message = await ApiProblemReader.ReadMessageAsync(response, default, "Handlingen", "udføres");
        throw new ApiException(message, response.StatusCode);
    }
}

public sealed record DeviceOverview(List<DeviceCustomer> Customers, List<DeviceAuction> Auctions, List<AvailableDevice> Available, List<DeviceSessionItem> Sessions);
public sealed record DeviceCustomer(Guid CustomerId, string FirstName, string LastName, string Email);
public sealed record DeviceAuction(Guid AuctionId, Guid AuctionHouseId, int AuctionNumber, string Name, DateTime StartsAt, string AuctionStatus);
public sealed record AvailableDevice(Guid DeviceId, Guid AuctionHouseId, int DeviceNumber);
public sealed record DeviceSessionItem(Guid SessionId, Guid DeviceId, int DeviceNumber, Guid AuctionId, Guid CustomerId, DateTime StartedAt, DateTime? EndedAt);
public sealed record DeviceRegistration(Guid DeviceId, Guid AuctionHouseId, int DeviceNumber, string Secret);
