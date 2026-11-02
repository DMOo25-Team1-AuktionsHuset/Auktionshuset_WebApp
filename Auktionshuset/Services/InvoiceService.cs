using Auktionshuset.Contracts.Dto.Billing;

namespace Auktionshuset.Services;

public sealed class InvoiceService(BackendApiClient client)
{
    public async Task<List<InvoiceResponse>> GetAsync(bool staff = false, CancellationToken cancellationToken = default)
    {
        using var response = await client.GetAsync(staff ? "api/staff/invoices/" : "api/customer/invoices", cancellationToken);
        await CheckAsync(response);
        return await response.Content.ReadFromJsonAsync<List<InvoiceResponse>>() ?? [];
    }

    public async Task<InvoiceResponse> GetOwnAsync(Guid id, Guid? receiptLine = null)
    {
        using var response = await client.GetAsync(receiptLine is Guid line
            ? $"api/customer/invoices/{id}/lines/{line}/receipt" : $"api/customer/invoices/{id}");
        await CheckAsync(response);
        return await response.Content.ReadFromJsonAsync<InvoiceResponse>() ?? throw new HttpRequestException("Faktura mangler.");
    }

    public async Task PayAsync(InvoiceResponse invoice, string method)
    {
        using var response = await client.PostAsJsonAsync($"api/staff/invoices/{invoice.Id}/payment",
            new RegisterInvoicePayment(method, invoice.Total));
        await CheckAsync(response);
    }

    public async Task CollectAsync(Guid invoiceId, Guid lineId)
    {
        using var response = await client.PostAsJsonAsync($"api/staff/invoices/{invoiceId}/lines/{lineId}/collection", new { });
        await CheckAsync(response);
    }

    private static async Task CheckAsync(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
            throw new CustomerApiException(await ApiProblemReader.ReadMessageAsync(response, default, "Fakturaen", "hentes eller opdateres"), response.StatusCode);
    }
}
