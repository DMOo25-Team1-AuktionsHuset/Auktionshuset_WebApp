using Auktionshuset.Api.Endpoints.Admin.CreateAuction;
using Auktionshuset.Api.Endpoints.Admin.CreateLot;
using Auktionshuset.Api.Events.Admin.Auction;
using Auktionshuset.Api.Events.Admin.Lot;
using Auktionshuset.Api.Hubs;
using Auktionshuset.Api.Services;
using Auktionshuset.Application.Abstraction.Admin.Auctions;
using Auktionshuset.Application.Abstraction.Admin.Lots;
using Auktionshuset.Application.Admin.Lots;
using Auktionshuset.Application.Admin.Lots.UpdateLot;
using Auktionshuset.Application.Admin.Lots.CreateLot;
using Auktionshuset.Application.Admin.Lots.DeleteLot;
using Auktionshuset.Application.EventHandling;
using Auktionshuset.Infrastructure.Service;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddValidation();
builder.Services.AddSignalR();

builder.Services.AddSingleton<ILotRepository, InMemoryLotRepository>();

//API Services
builder.Services.AddApiServices();
builder.Services.AddSingleton<IAuctionRepository, InMemoryAuctionRepository>();
builder.Services.AddScoped<CreateAuctionHandler>();


//builder.Services.AddScoped<
//    IIntegrationEventPublisher, 
//    InProcessIntegrationEventPublisher>();

builder.Services.AddScoped<
    IIntegrationEventHandler<LotCreatedIntegrationEvent>,
    CreateLotRealTimeHandler>();

builder.Services.AddScoped<
    IIntegrationEventHandler<AuctionCreatedIntegrationEvent>,
    CreateAuctionRealTimeHandler>();

//Infrastructure Services
builder.Services.AddInfrastructure();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapLotEndpoints();
app.MapAuctionEndpoints();
app.MapHub<LotHub>("/hubs/lot");
app.MapHub<AuctionHub>("/hubs/auction");

app.Run();

