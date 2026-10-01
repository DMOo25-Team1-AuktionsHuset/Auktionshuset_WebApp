using Auktionshuset.Api.Endpoints.Admin.CreateAuction;
using Auktionshuset.Api.Events.Admin.Auction;
using Auktionshuset.Api.Endpoints.Auth;
using Auktionshuset.Api.Endpoints.Admin.Employee.GetEmployees;
using Auktionshuset.Api.Endpoints.Admin.Lots;
using Auktionshuset.Api.Endpoints.Admin.Employee;
using Auktionshuset.Api.Endpoints.Admin.LotImage;
using Auktionshuset.Api.Extensions;
using Auktionshuset.Api.Hubs;
using Auktionshuset.Api.Security;
using Auktionshuset.Api.Services;
using Auktionshuset.Application.Abstraction.Admin.Auctions;
using Auktionshuset.Application.Abstraction.Admin.Employees;
using Auktionshuset.Application.Admin.Auctions.CreateAuction;
using Auktionshuset.Application.EventHandling;
using Auktionshuset.Infrastructure.Service;


WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddValidation();
builder.Services.AddSignalR();

builder.Services.AddSecurityServices(builder.Configuration);



// needs its own service class
builder.Services.AddScoped<CreateAuctionHandler>();

builder.Services.AddSingleton<IEmployeeRepository, InMemoryEmployeeRepository>();
builder.Services.AddSingleton<IAuctionRepository, InMemoryAuctionRepository>();

//API Services
builder.Services.AddApiServices();

builder.Services.AddScoped<
    IIntegrationEventHandler<AuctionCreatedIntegrationEvent>,
    CreateAuctionRealTimeHandler>();

//Infrastructure Services
builder.Services.AddInfrastructure(builder.Configuration);

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

app.MapHub<AuctionHub>("/hubs/auction", options => options.CloseOnAuthenticationExpiration = true)
    .RequireAuthorization(SecurityPolicies.CanCreateAuction);
app.MapHub<EmployeeHub>("/hubs/employee", options => options.CloseOnAuthenticationExpiration = true)
    .RequireAuthorization(SecurityPolicies.CanReadEmployees);
app.MapHub<LotHub>("/hubs/lot", options => options.CloseOnAuthenticationExpiration = true)
    .RequireAuthorization(SecurityPolicies.CanViewLots);

app.Run();
