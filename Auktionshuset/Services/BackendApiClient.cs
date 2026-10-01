using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using Auktionshuset.Security;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;

namespace Auktionshuset.Services;

public sealed class BackendApiClient(
    HttpClient httpClient,
    BackendTokenAccessor tokenAccessor,
    NavigationManager navigationManager,
    IHttpContextAccessor httpContextAccessor,
    FrontendRedirectState redirectState) : IDisposable
{
    private readonly CancellationTokenSource sessionCancellation = new();
    private int redirectStarted;
    public Uri? BaseAddress => httpClient.BaseAddress;

    public Task<HttpResponseMessage> GetAsync(string requestUri, CancellationToken cancellationToken = default) =>
        SendAsync(new HttpRequestMessage(HttpMethod.Get, requestUri), cancellationToken);

    public Task<HttpResponseMessage> PostAsJsonAsync<T>(string requestUri, T value, CancellationToken cancellationToken = default) =>
        SendAsync(new HttpRequestMessage(HttpMethod.Post, requestUri)
        {
            Content = JsonContent.Create(value)
        }, cancellationToken);

    public Task<HttpResponseMessage> PutAsJsonAsync<T>(string requestUri, T value, CancellationToken cancellationToken = default) =>
        SendAsync(new HttpRequestMessage(HttpMethod.Put, requestUri)
        {
            Content = JsonContent.Create(value)
        }, cancellationToken);

    public Task<HttpResponseMessage> DeleteAsync(string requestUri, CancellationToken cancellationToken = default) =>
        SendAsync(new HttpRequestMessage(HttpMethod.Delete, requestUri), cancellationToken);

    public Task<HttpResponseMessage> PostAsync(string requestUri, HttpContent content, CancellationToken cancellationToken = default) =>
        SendAsync(new HttpRequestMessage(HttpMethod.Post, requestUri) { Content = content }, cancellationToken);

    private async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        string? accessToken = await tokenAccessor.GetAccessTokenAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            await ExpireSessionAsync();
            request.Dispose();
            return new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent(string.Empty)
            };
        }

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            sessionCancellation.Token);
        HttpResponseMessage response;
        using (request)
        {
            response = await httpClient.SendAsync(request, linkedCancellation.Token);
        }

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            await ExpireSessionAsync();
        }
        else if (response.StatusCode == HttpStatusCode.Forbidden
            && Interlocked.Exchange(ref redirectStarted, 1) == 0)
        {
            Redirect("/?access=denied");
        }

        return response;
    }

    private async Task ExpireSessionAsync()
    {
        if (Interlocked.Exchange(ref redirectStarted, 1) != 0)
        {
            return;
        }

        await tokenAccessor.InvalidateCurrentSessionAsync();
        sessionCancellation.Cancel();
        Redirect("/login");
    }

    private void Redirect(string location)
    {
        redirectState.RequestRedirect(location);
        HttpContext? context = httpContextAccessor.HttpContext;
        if (context is not null && !context.Response.HasStarted)
        {
            context.Items[FrontendRedirectMiddleware.RedirectItemKey] = location;
            return;
        }

        navigationManager.NavigateTo(location, forceLoad: true);
    }

    public void Dispose() => sessionCancellation.Dispose();
}
