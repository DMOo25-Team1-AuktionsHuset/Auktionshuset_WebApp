using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json;
using Auktionshuset.Contracts.Dto.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Antiforgery;

namespace Auktionshuset.Security;

public static class LocalAuthEndpoints
{
    public static IEndpointRouteBuilder MapLocalAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/auth/login", LoginAsync).AllowAnonymous();
        endpoints.MapPost("/auth/logout", LogoutAsync).AllowAnonymous();
        return endpoints;
    }

    private static async Task<IResult> LoginAsync(
        HttpContext context,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        IAntiforgery antiforgery,
        CancellationToken cancellationToken)
    {
        try
        {
            await antiforgery.ValidateRequestAsync(context);
        }
        catch (AntiforgeryValidationException)
        {
            return Results.BadRequest("Ugyldig formular. Genindlæs siden og prøv igen.");
        }

        IFormCollection form = await context.Request.ReadFormAsync(cancellationToken);
        string email = form["email"].ToString().Trim();
        string password = form["password"].ToString();
        if (string.IsNullOrWhiteSpace(email)
            || !new EmailAddressAttribute().IsValid(email)
            || string.IsNullOrEmpty(password))
        {
            return Results.Redirect("/login?error=invalid");
        }

        string apiBaseUrl = configuration["Api:BaseUrl"]
            ?? throw new InvalidOperationException("Configuration value 'Api:BaseUrl' is required.");
        using var client = httpClientFactory.CreateClient("BackendAuthentication");
        client.BaseAddress = new Uri(apiBaseUrl);

        LoginResponse? login;
        try
        {
            using HttpResponseMessage response = await client.PostAsJsonAsync(
                "api/auth/login",
                new LoginRequest { Email = email, Password = password },
                cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                return Results.Redirect("/login?error=invalid");
            }

            if (!response.IsSuccessStatusCode)
            {
                return Results.Redirect("/login?error=unavailable");
            }

            login = await response.Content.ReadFromJsonAsync<LoginResponse>(cancellationToken);
        }
        catch (HttpRequestException)
        {
            return Results.Redirect("/login?error=unavailable");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Results.Redirect("/login?error=unavailable");
        }
        catch (JsonException)
        {
            return Results.Redirect("/login?error=unavailable");
        }

        if (login is null
            || string.IsNullOrWhiteSpace(login.AccessToken)
            || login.ExpiresAtUtc <= DateTimeOffset.UtcNow
            || login.User is null
            || login.User.UserId == Guid.Empty
            || string.IsNullOrWhiteSpace(login.User.Email))
        {
            return Results.Redirect("/login?error=unavailable");
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, login.User.UserId.ToString()),
            new(ClaimTypes.Email, login.User.Email),
            new(ClaimTypes.Name, login.User.Email)
        };
        claims.AddRange(login.User.Roles.Select(role => new Claim(ClaimTypes.Role, role)));
        claims.AddRange(login.User.Permissions.Select(permission => new Claim("permission", permission)));

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        var properties = new AuthenticationProperties
        {
            IsPersistent = true,
            AllowRefresh = false,
            IssuedUtc = DateTimeOffset.UtcNow,
            ExpiresUtc = login.ExpiresAtUtc
        };
        properties.Items[ServerTicketStore.BackendTokenProperty] = login.AccessToken;

        await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, properties);
        return Results.Redirect("/");
    }

    private static async Task<IResult> LogoutAsync(HttpContext context, IAntiforgery antiforgery)
    {
        try
        {
            await antiforgery.ValidateRequestAsync(context);
        }
        catch (AntiforgeryValidationException)
        {
            return Results.BadRequest("Ugyldig formular. Genindlæs siden og prøv igen.");
        }

        await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Results.Redirect("/login");
    }
}
