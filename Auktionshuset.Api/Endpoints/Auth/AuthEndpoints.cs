using Auktionshuset.Api.Security;
using Auktionshuset.Api.Services;
using Auktionshuset.Contracts.Dto.Auth;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;


namespace Auktionshuset.Api.Endpoints.Auth
{
    public static class AuthEndpoints
    {

        public static IEndpointRouteBuilder MapAuthEndpoints(
            this IEndpointRouteBuilder endpoints)
        {
            RouteGroupBuilder group = endpoints
                .MapGroup("/api/auth")
                .WithTags("Authentication")
                .AllowAnonymous();

            group.MapPost("/login", HandleLoginAsync)
                .RequireRateLimiting(AuthRateLimitingExtensions.Login)
                .WithName("Login")
                .Produces<LoginResponse>()
                .Produces(StatusCodes.Status401Unauthorized)
                .Produces(StatusCodes.Status429TooManyRequests)
                .ProducesValidationProblem();

            group.MapPost("/register", HandleRegisterAsync)
                .RequireRateLimiting(AuthRateLimitingExtensions.Register)
                .WithName("RegisterCustomer")
                .Produces<RegisterCustomerResponse>(StatusCodes.Status201Created)
                .Produces(StatusCodes.Status409Conflict)
                .Produces(StatusCodes.Status429TooManyRequests)
                .ProducesValidationProblem();

            group.MapPost("/refresh", async (RefreshTokenRequest request, RefreshTokenService tokens,
                HttpContext context, CancellationToken cancellationToken) =>
            {
                DisableCaching(context);
                LoginResponse? response = await tokens.RotateAsync(request.RefreshToken, cancellationToken);
                return response is null ? Results.Unauthorized() : Results.Ok(response);
            }).RequireRateLimiting(AuthRateLimitingExtensions.Refresh)
                .Produces<LoginResponse>().Produces(StatusCodes.Status401Unauthorized)
                .Produces(StatusCodes.Status429TooManyRequests);

            // Possession of the refresh token identifies the session, even if the JWT has expired.
            group.MapPost("/logout", async (RefreshTokenRequest request, RefreshTokenService tokens,
                HttpContext context, CancellationToken cancellationToken) =>
            {
                DisableCaching(context);
                await tokens.RevokeAsync(request.RefreshToken, cancellationToken);
                return Results.NoContent();
            }).RequireRateLimiting(AuthRateLimitingExtensions.Logout)
                .Produces(StatusCodes.Status204NoContent);

            return endpoints;
        }

        public static async Task<Results<
            Ok<LoginResponse>,
            UnauthorizedHttpResult>> HandleLoginAsync(
            LoginRequest request, 
            IAuthUserStore userStore,
            RefreshTokenService refreshTokens,
            HttpContext context,
            CancellationToken cancellationToken)
        {
            AuthUser? user = await userStore.FindByEmailAsync(
                request.Email, cancellationToken);

            DisableCaching(context);
            if (user is null)
            {
                return TypedResults.Unauthorized();
            }

            if (!await userStore.CheckPasswordAsync(user, request.Password, cancellationToken))
            {
                return TypedResults.Unauthorized();
            }

            LoginResponse response = await refreshTokens.CreateSessionAsync(user, cancellationToken);

            return TypedResults.Ok(response);
        }

        public static async Task<Results<
            Created<RegisterCustomerResponse>,
            Conflict<ProblemDetails>,
            ValidationProblem>> HandleRegisterAsync(
            RegisterCustomerRequest request,
            CustomerRegistrationService registrations,
            HttpContext context,
            CancellationToken cancellationToken)
        {
            DisableCaching(context);
            if (RegisterCustomerValidation.Validate(request) is ValidationProblem invalid)
            {
                return invalid;
            }

            RegistrationOutcome outcome = await registrations.RegisterAsync(request, cancellationToken);
            return outcome.Failure == RegistrationFailure.DuplicateEmail
                ? TypedResults.Conflict(RegisterCustomerValidation.DuplicateEmailProblem())
                : TypedResults.Created("/api/auth/register", outcome.Account!);
        }

        private static void DisableCaching(HttpContext context)
        {
            context.Response.Headers.CacheControl = "no-store";
            context.Response.Headers.Pragma = "no-cache";
        }
    }
}
