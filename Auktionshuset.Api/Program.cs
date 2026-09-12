using Auktionshuset.Api.Endpoints.Admin.CreateLot;
using Auktionshuset.Api.Events.Admin.Lot;
using Auktionshuset.Api.Hubs;
using Auktionshuset.Application.Abstraction.Admin.Lots;
using Auktionshuset.Application.EventHandling;
using Auktionshuset.Infrastructure.EventHandling;
using Auktionshuset.Infrastructure.Service;

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


builder.Services.AddScoped<
    IIntegrationEventPublisher,
    InProcessIntegrationEventPublisher>();

builder.Services.AddScoped<
    IIntegrationEventHandler<LotCreatedIntegrationEvent>,
    CreateLotRealTimeHandler>();

//Infrastructure Services

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapLotEndpoints();
app.MapHub<LotHub>("/hubs/lot");

app.Run();
