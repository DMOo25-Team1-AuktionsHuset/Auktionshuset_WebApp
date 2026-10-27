using Auktionshuset.Api.Endpoints.Admin.Auction.CloseAuction;
using Auktionshuset.Api.Endpoints.Admin.Bid;
using Auktionshuset.Api.Endpoints.Admin.CreateAuction;
using Auktionshuset.Api.Endpoints.Admin.Employee;
using Auktionshuset.Api.Endpoints.Admin.LotImage;
using Auktionshuset.Api.Endpoints.Admin.Lots;
using Auktionshuset.Api.Endpoints.Admin.Messaging;
using Auktionshuset.Api.Endpoints.Auth;
using Auktionshuset.Api.Extensions;
using Auktionshuset.Api.Hubs;
using Auktionshuset.Api.Security;
using Auktionshuset.Api.Services;
using Auktionshuset.Application.Abstraction.Admin.Auctions;
using Auktionshuset.Application.Abstraction.Admin.Employees;
using Auktionshuset.Infrastructure.Service;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddValidation();
builder.Services.AddSignalR();

builder.Services.AddSecurityServices(builder.Configuration);

//API Services
builder.Services.AddApiServices();

//Infrastructure Services
await builder.Services.AddInfrastructureAsync(builder.Configuration);

if (builder.Configuration.GetValue<bool>(
        "Observability:EnableOtlp"))
{
    builder.Services.AddOpenTelemetry()
        .ConfigureResource(resource => resource.AddService(
            serviceName: "Auktionshuset.Api",
            serviceInstanceId: Guid.NewGuid().ToString("N")))
        .WithMetrics(metrics => metrics
            .AddMeter("Auktionshuset.Messaging")
            .AddOtlpExporter());
}

WebApplication app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

await app.MigrateAndSeedDatabaseAsync();

app.UseForwardedHeaders();
app.UseHttpsRedirection();
app.UseRouting();
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapAuthEndpoints();
app.MapLotEndpoints();
app.MapStoredLotImages();
app.MapAuctionEndpoints();
app.MapEmployeeEndpoints();
app.MapBidEndpoints();
app.MapCloseAuctionLotEndpoint();

app.MapMessagingEndpoints();


app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();

app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = registration => registration.Tags.Contains("ready") }).AllowAnonymous();

app.MapHealthChecks("/health/messaging", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("messaging"),
    ResponseWriter = async (context, report) =>
        {
            await context.Response.WriteAsJsonAsync(new
            {
                status = report.Status.ToString(),

                checks = report.Entries.Select(entry => new
                {
                    name = entry.Key,
                    status = entry.Value.Status.ToString(),
                    description = entry.Value.Description,
                    data = entry.Value.Data
                })
            }, cancellationToken: context.RequestAborted);
        }
}).RequireAuthorization(SecurityPolicies.Admin);


app.MapHub<AuctionHub>("/hubs/auction", options => options.CloseOnAuthenticationExpiration = true)
    .RequireAuthorization(SecurityPolicies.CanViewAuctions);
app.MapHub<EmployeeHub>("/hubs/employee", options => options.CloseOnAuthenticationExpiration = true)
    .RequireAuthorization(SecurityPolicies.CanViewEmployees);
app.MapHub<LotHub>("/hubs/lot", options => options.CloseOnAuthenticationExpiration = true)
    .RequireAuthorization(SecurityPolicies.CanViewLots);

app.Run();
