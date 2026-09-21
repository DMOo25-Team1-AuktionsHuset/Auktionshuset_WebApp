using Auktionshuset.Api.Endpoints.Admin.CreateAuction;
using Auktionshuset.Api.Endpoints.Admin.CreateLot;
using Auktionshuset.Api.Endpoints.Admin.GetEmployees;
using Auktionshuset.Api.Hubs;
using Auktionshuset.Api.Services;
using Auktionshuset.Application.Abstraction.Admin.Auctions;
using Auktionshuset.Application.Abstraction.Admin.Lots;
using Auktionshuset.Contracts.Dto.Admin.Lot.Image;
using Auktionshuset.Infrastructure.Service;
using Auktionshuset.Infrastructure.Service.Lots;
using Microsoft.Extensions.FileProviders;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Auktionshuset.Application.Admin.Auctions.CreateAuction;

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
builder.Services.AddSingleton<IAuctionRepository, InMemoryAuctionRepository>();

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

// Serve the uploaded lot images from the same folder the image store writes to.
var imageStoreOptions = app.Services.GetRequiredService<LotImageStoreOptions>();
Directory.CreateDirectory(imageStoreOptions.RootPath);

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(imageStoreOptions.RootPath),
    RequestPath = LotImagePaths.RequestPath
});

app.UseAuthentication();
app.UseAuthorization();

app.MapLotEndpoints();
app.MapHub<LotHub>("/hubs/lot")
    .AllowAnonymous();
app.MapAuctionEndpoints();
app.MapEmployeeEndpoints();
app.MapHub<AuctionHub>("/hubs/auction");

app.Run();
