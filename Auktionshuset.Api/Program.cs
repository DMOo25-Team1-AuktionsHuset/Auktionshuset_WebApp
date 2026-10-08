using Auktionshuset.Api.Endpoints.Auth;
using Auktionshuset.Api.Endpoints.Admin.Lots;
using Auktionshuset.Api.Endpoints.Admin.Employee;
using Auktionshuset.Api.Endpoints.Admin.LotImage;
using Auktionshuset.Api.Extensions;
using Auktionshuset.Api.Hubs;
using Auktionshuset.Api.Security;
using Auktionshuset.Api.Services;
using Auktionshuset.Application.Abstraction.Admin.Auctions;
using Auktionshuset.Application.Abstraction.Admin.Employees;
using Auktionshuset.Infrastructure.Service;
using Auktionshuset.Infrastructure.Service.Auctions;
using Auktionshuset.Application.Abstraction.Auction;
using Auktionshuset.Api.Endpoints.Admin.Bid;
using Auktionshuset.Application.Admin.Auctions.CloseAuction;
using Auktionshuset.Api.Endpoints.Admin.Auction;

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
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddScoped<AuctionLotBiddingStore>();
builder.Services.AddScoped<CloseAuctionLotHandler>();
builder.Services.AddScoped<IAuctionLifeCycleStore, AuctionLifeCycleStore>();

builder.Services.AddScoped<IPlaceBidStore>(
    sp => sp.GetRequiredService<AuctionLotBiddingStore>());

builder.Services.AddScoped<ICloseAuctionLotStore>(
    sp => sp.GetRequiredService<AuctionLotBiddingStore>());

WebApplication app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

await app.MigrateAndSeedDatabaseAsync();

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapAuthEndpoints();
app.MapLotEndpoints();
app.MapStoredLotImages();
app.MapAuctionEndpoints();
app.MapEmployeeEndpoints();
app.MapBidEndpoints();

app.MapHub<AuctionHub>("/hubs/auction");
app.MapHub<EmployeeHub>("/hubs/employee");
app.MapHub<LotHub>("/hubs/lot")
    .RequireAuthorization(SecurityPolicies.Admin);

app.Run();
