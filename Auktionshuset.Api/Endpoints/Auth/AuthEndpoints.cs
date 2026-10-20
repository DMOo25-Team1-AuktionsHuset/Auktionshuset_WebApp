using Auktionshuset.Api.Security;
using Auktionshuset.Contracts.Dto.Auth;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;


namespace Auktionshuset.Api.Endpoints.Auth
{
    public static class AuthEndpoints
    {

        public static IEndpointRouteBuilder MapAuthEndpoints(
            this IEndpointRouteBuilder endpoints)
        {
            var group = endpoints
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

            group.MapPost("/refresh", async (RefreshTokenRequest request, RefreshTokenService tokens,
                HttpContext context, CancellationToken cancellationToken) =>
            {
                DisableCaching(context);
                var response = await tokens.RotateAsync(request.RefreshToken, cancellationToken);
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
            IPasswordHasher<AuthUser> passwordHasher,
            RefreshTokenService refreshTokens,
            HttpContext context,
            CancellationToken cancellationToken)
        {
            var user = await userStore.FindByEmailAsync(
                request.Email, cancellationToken);

            DisableCaching(context);
            if (user is null)
            {
                return TypedResults.Unauthorized();
            }

            var verificationResult = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);

            if (verificationResult == PasswordVerificationResult.Failed)
            {
                return TypedResults.Unauthorized();
            }

            if (verificationResult == PasswordVerificationResult.SuccessRehashNeeded)
            {
                user.PasswordHash = passwordHasher.HashPassword(
                    user, 
                    request.Password);
            }

            var response = await refreshTokens.CreateSessionAsync(user, cancellationToken);

            return TypedResults.Ok(response);
        }

        private static void DisableCaching(HttpContext context)
        {
            context.Response.Headers.CacheControl = "no-store";
            context.Response.Headers.Pragma = "no-cache";
        }
    }
}
