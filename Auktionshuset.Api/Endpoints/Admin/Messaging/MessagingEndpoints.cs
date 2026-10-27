using System.Security.Claims;
using System.Text.Json.Serialization;
using Auktionshuset.Api.Security;
using Auktionshuset.Application.Abstraction;
using Auktionshuset.Contracts.Time;

namespace Auktionshuset.Api.Endpoints.Admin.Messaging
{
    public static class MessagingEndpoints
    {
        public static IEndpointRouteBuilder MapMessagingEndpoints(
            this IEndpointRouteBuilder endpoints)
        {
            RouteGroupBuilder group = endpoints
                .MapGroup("/api/admin/messaging")
                .RequireAuthorization(SecurityPolicies.Admin);

            group.MapGet("/status", (IMessagingDiagnostics diagnostics) =>
                Results.Ok(diagnostics.Read()));

            group.MapGet("/quarantine", async (
                string pipeline,
                int? skip,
                int? take,
                IMessagingOperations operations,
                CancellationToken cancellationToken) =>
            {
                int offset = skip ?? 0;
                int pageSize = take ?? 50;

                if (!ValidPipeline(pipeline)
                    || offset < 0
                    || pageSize is < 1 or > 100)
                {
                    return Results.BadRequest(new
                    {
                        error = "Brug pipeline=outbox/inbox, " +
                                "skip>=0 og take mellem 1 og 100."
                    });
                }

                return Results.Ok(
                    await operations.GetQuarantineAsync(
                        pipeline,
                        offset,
                        pageSize,
                        cancellationToken));
            });

            group.MapGet("/messages/{pipeline}/{messageId:guid}", async (
                string pipeline,
                Guid messageId,
                IMessagingOperations operations,
                CancellationToken cancellationToken) =>
            {
                if (!ValidPipeline(pipeline))
                    return Results.BadRequest();

                MessageDetails? details = await operations.GetMessageAsync(
                    pipeline,
                    messageId,
                    cancellationToken);

                return details is null
                    ? Results.NotFound()
                    : Results.Ok(details);
            });

            group.MapPost("/{pipeline}/{messageId:guid}/replay", async (
                string pipeline,
                Guid messageId,
                ReplayRequest request,
                ClaimsPrincipal user,
                IMessagingOperations operations,
                CancellationToken cancellationToken) =>
            {
                if (!ValidPipeline(pipeline)
                    || request.ExpectedQuarantinedAtTime is null)
                {
                    return Results.BadRequest(new
                    {
                        error = "Pipeline og expectedQuarantinedAtTime er påkrævet."
                    });
                }

                string? actorId =
                    user.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? user.FindFirstValue("sub");

                if (string.IsNullOrWhiteSpace(actorId))
                    return Results.Forbid();

                ReplayResult result = await operations.ReplayAsync(
                    pipeline,
                    messageId,
                    request.ExpectedQuarantinedAtTime.Value.UtcDateTime,
                    actorId,
                    request.Reason ?? "",
                    cancellationToken);

                if (result.StatusCode == StatusCodes.Status202Accepted)
                {
                    return Results.Accepted(
                        $"/api/admin/messaging/messages/{pipeline}/{messageId}",
                        result);
                }

                return Results.Problem(
                    statusCode: result.StatusCode,
                    detail: result.Detail);
            });

            return endpoints;
        }

        private static bool ValidPipeline(string pipeline)
            => pipeline is "outbox" or "inbox";
    }

    public sealed record ReplayRequest(
        string? Reason,

        [property: JsonConverter(typeof(OffsetDateTimeConverter))]
        DateTimeOffset? ExpectedQuarantinedAtTime);
}
