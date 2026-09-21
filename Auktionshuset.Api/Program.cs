using Auktionshuset.Api.Endpoints.Admin.CreateAuction;
using Auktionshuset.Api.Events.Admin.Auction;
using Auktionshuset.Api.Events.Admin.Lot;
using Auktionshuset.Api.Hubs;
using Auktionshuset.Api.Security;
using Auktionshuset.Api.Services;
using Auktionshuset.Application.Abstraction.Admin.Auctions;
using Auktionshuset.Application.Abstraction.Admin.Lots;
using Auktionshuset.Application.Abstraction.Admin.Employees;
using Auktionshuset.Application.Admin.Lots;
using Auktionshuset.Application.Admin.Lots.UpdateLot;
using Auktionshuset.Application.Admin.Lots.CreateLot;
using Auktionshuset.Application.Admin.Lots.DeleteLot;
using Auktionshuset.Application.EventHandling;
using Auktionshuset.Infrastructure.Service;
using Auktionshuset.Application.Admin.Auctions.CreateAuction;
using Auktionshuset.Api.Endpoints.Admin.Lots;
using Auktionshuset.Api.Events.Admin.Employee;
using Auktionshuset.Application.Admin.Employees.DeleteEmployee;
using Auktionshuset.Api.Endpoints.Admin.Employee;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddValidation();
builder.Services.AddSignalR();

builder.Services.AddSecurityServices(builder.Configuration);



// needs its own service class
builder.Services.AddScoped<CreateAuctionHandler>();

builder.Services.AddSingleton<ILotRepository, InMemoryLotRepository>();
builder.Services.AddSingleton<IEmployeeRepository, InMemoryEmployeeRepository>();

//API Services
builder.Services.AddApiServices();
builder.Services.AddSingleton<IAuctionRepository, InMemoryAuctionRepository>();

builder.Services.AddScoped<
    IIntegrationEventHandler<AuctionCreatedIntegrationEvent>,
    CreateAuctionRealTimeHandler>();

//Infrastructure Services
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapLotEndpoints();
app.MapHub<LotHub>("/hubs/lot")
    .RequireAuthorization(SecurityPolicies.Admin);
app.MapAuctionEndpoints();
app.MapHub<AuctionHub>("/hubs/auction");
app.MapEmployeeEndpoints();
app.MapHub<EmployeeHub>("/hubs/employee");

app.Run();

