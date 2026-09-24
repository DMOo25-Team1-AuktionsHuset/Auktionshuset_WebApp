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
                .WithName("Login")
                .Produces<LoginResponse>()
                .Produces(StatusCodes.Status401Unauthorized)
                .ProducesValidationProblem();

            return endpoints;
        }

        public static async Task<Results<
            Ok<LoginResponse>,
            UnauthorizedHttpResult>> HandleLoginAsync(
            LoginRequest request, 
            IAuthUserStore userStore,
            IPasswordHasher<AuthUser> passwordHasher,
            IAccessTokenService tokenService,
            CancellationToken cancellationToken)
        {
            var user = await userStore.FindByEmailAsync(
                request.Email, cancellationToken);

            if(user is null)
            {
                return TypedResults.Unauthorized();
            }

            var verificationResult = passwordHasher.VerifyHashedPassword(
                user,
                user.PasswordHash,
                request.Password);

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

            var token = tokenService.Issue(user);

            var response = new LoginResponse(
                token.Value,
                "Bearer",
                token.ExpiresAtUtc,
                new AuthenticatedUserResponse(
                    user.UserId,
                    user.Email,
                    user.Roles,
                    user.Permissions));

            return TypedResults.Ok(response);
        }
    }
}
