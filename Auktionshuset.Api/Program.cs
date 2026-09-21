using Auktionshuset.Api.Endpoints.Admin.CreateAuction;
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
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Auktionshuset.Application.Admin.Auctions.CreateAuction;
using Auktionshuset.Api.Endpoints.Admin.Lot.CreateLot;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddValidation();
builder.Services.AddSignalR();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var signingKey = builder.Configuration["Authentication:SigningKey"]
            ?? throw new InvalidOperationException(
                "Missing configuration value 'Authentication:SigningKey'.");

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Authentication:Issuer"],

            ValidateAudience = true,
            ValidAudience = builder.Configuration["Authentication:Audience"],

            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(signingKey)),

            ValidateLifetime = true
        };
    });

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

    options.AddPolicy("CanViewLots", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireClaim("permission", "lots.read");
    });
});


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
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapLotEndpoints();
app.MapHub<LotHub>("/hubs/lot")
    .AllowAnonymous();
app.MapAuctionEndpoints();
app.MapHub<AuctionHub>("/hubs/auction");

app.Run();

