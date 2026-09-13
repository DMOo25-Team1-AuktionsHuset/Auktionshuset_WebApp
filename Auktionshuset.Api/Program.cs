using Auktionshuset.Api.Endpoints.Admin;
using Auktionshuset.Api.Events;
using Auktionshuset.Api.Hubs;
using Auktionshuset.Api.Events.Admin.Lot;
using Auktionshuset.Api.Endpoints.Admin.CreateLot;
using Auktionshuset.Application.Abstraction.Admin.Lots;
using Auktionshuset.Infrastructure.Service;
using Auktionshuset.Infrastructure;
using Auktionshuset.Application.EventHandling;
using Auktionshuset.Infrastructure.EventHandling;
using Auktionshuset.Infrastructure.Messaging;
using RabbitMQ.Client;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddValidation();
builder.Services.AddSignalR();

builder.Services.AddSingleton<ILotRepository, InMemoryLotRepository>();
builder.Services.AddScoped<CreateLotHandler>();
builder.Services.AddScoped<GetLotsHandler>();


//builder.Services.AddScoped<
//    IIntegrationEventPublisher, 
//    InProcessIntegrationEventPublisher>();

builder.Services.AddScoped<
    IIntegrationEventHandler<LotCreatedIntegrationEvent>, 
    CreateLotRealTimeHandler>();

//Infrastructure Services
builder.Services.AddInfrastructure();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment()) {
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapLotEndpoints();
app.MapHub<LotHub>("/hubs/lot");

app.Run();
