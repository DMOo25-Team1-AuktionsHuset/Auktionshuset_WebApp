using Auktionshuset.Api.Endpoints.Admin.CreateLot;
using Auktionshuset.Api.Events.Admin.Lot;
using Auktionshuset.Api.Hubs;
using Auktionshuset.Api.Services;
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

//builder.Services.AddAuthentication(builder.Configuration)
//    .AddJwtBearer

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Admin", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireRole("Admin");
    });

    options.AddPolicy("CanCreateLot", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireClaim("permission", "lots.create");
    });

    options.AddPolicy("CanUpdateLot", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireClaim("permission", "lots.update");
    });

    options.AddPolicy("CanDeleteLot", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireClaim("permission", "lots.delete");
    });
});


builder.Services.AddSingleton<ILotRepository, InMemoryLotRepository>();

//API Services
builder.Services.AddApiServices();

//Infrastructure Services
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapLotEndpoints();
app.MapHub<LotHub>("/hubs/lot")
    .RequireAuthorization("Admin");

app.Run();

