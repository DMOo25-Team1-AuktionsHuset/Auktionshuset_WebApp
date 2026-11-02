using Auktionshuset.Contracts.Dto.Customer.Response;
using Auktionshuset.Contracts.Dto.Auction;
namespace Auktionshuset.Services;

public sealed class CustomerAuctionService(BackendApiClient client)
{
    public async Task UnregisterAsync(Guid id)
    {
        using var response = await client.DeleteAsync($"api/customer/auctions/{id}/registration");
        await EnsureSuccessAsync(response);
    }
    public async Task<List<CustomerAuctionResponse>> GetMyAuctionsAsync(CancellationToken cancellationToken = default)
    {
        using var response = await client.GetAsync("api/customer/my-auctions", cancellationToken);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<List<CustomerAuctionResponse>>() ?? [];
    }
    public async Task<List<CustomerPurchaseResponse>> GetPurchasesAsync(CancellationToken cancellationToken = default)
    {
        using var response = await client.GetAsync("api/customer/purchases", cancellationToken);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<List<CustomerPurchaseResponse>>() ?? [];
    }
    public async Task<List<CustomerAuctionResponse>> GetAuctionsAsync(CancellationToken cancellationToken = default)
    {
        using var response = await client.GetAsync("api/customer/auctions", cancellationToken);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<List<CustomerAuctionResponse>>() ?? [];
    }
    public async Task RegisterAsync(Guid id)
    {
        using var response = await client.PostAsJsonAsync($"api/customer/auctions/{id}/registration", new { });
        await EnsureSuccessAsync(response);
    }
    public async Task<CustomerAuctionDetailResponse> GetAuctionAsync(Guid id)
    {
        using var response = await client.GetAsync($"api/customer/auctions/{id}");
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<CustomerAuctionDetailResponse>()
            ?? throw new CustomerApiException("Auktionen kunne ikke hentes.", response.StatusCode);
    }
    public async Task PlaceBidAsync(Guid lotId, Guid requestId, decimal amount)
    {
        using var response = await client.PostAsJsonAsync($"api/customer/lots/{lotId}/bids", new PlaceBidRequest(requestId, amount));
        if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            var result = await response.Content.ReadFromJsonAsync<BidError>();
            string message = result?.ErrorCode switch
            {
                "BidTooLow" => $"Buddet er for lavt. Den aktuelle pris er {result.CurrentPrice:N2} kr.",
                "BiddingClosed" => "Der er lukket for bud på denne genstand.",
                _ => "Buddet blev afvist. Opdatér siden og prøv igen."
            };
            throw new CustomerApiException(message, response.StatusCode);
        }
        await EnsureSuccessAsync(response);
    }
    private static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;
        string message = await ApiProblemReader.ReadMessageAsync(response, default, "Auktionen", "hentes eller opdateres");
        throw new CustomerApiException(message, response.StatusCode);
    }
    private sealed record BidError(string? ErrorCode, decimal CurrentPrice);
}
