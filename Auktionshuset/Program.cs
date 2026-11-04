using Auktionshuset.Contracts.Security;
using Auktionshuset.Components;
using Auktionshuset.Security;
using Auktionshuset.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using System.Net;
using System.Security.Claims;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddFrontendRateLimiting();
builder.Services.AddSingleton<ServerTicketStore>();
builder.Services.AddSingleton<BackendSessionService>();
builder.Services.AddSingleton<ITicketStore>(services => services.GetRequiredService<ServerTicketStore>());
builder.Services.AddScoped<AuthenticationStateProvider, SessionRevalidatingAuthenticationStateProvider>();
builder.Services.AddScoped<BackendTokenAccessor>();
builder.Services.AddScoped<FrontendRedirectState>();
builder.Services.AddScoped<AuctionRealtimeService>();
builder.Services.AddScoped<EmployeeRealtimeService>();
builder.Services.AddScoped<LotRealtimeService>();

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "Auktionshuset.Session";
        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
        options.SlidingExpiration = false;
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.Events.OnValidatePrincipal = async context =>
        {
            string? sessionId = context.Principal?.FindFirstValue(ServerTicketStore.SessionIdClaim);
            BackendSessionService sessions = context.HttpContext.RequestServices.GetRequiredService<BackendSessionService>();
            if (sessionId is null || await sessions.GetAccessTokenAsync(sessionId, context.HttpContext.RequestAborted) is null)
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return;
            }
            AuthenticationTicket? ticket = context.HttpContext.RequestServices.GetRequiredService<ServerTicketStore>().GetValidTicket(sessionId);
            if (ticket is not null) context.ReplacePrincipal(ticket.Principal);
        };
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.Redirect("/login");
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.Redirect(context.HttpContext.User.IsInRole(RoleNames.Customer)
                ? AuthDestination.CustomerHome + "?access=denied"
                : "/?access=denied");
            return Task.CompletedTask;
        };
    });
builder.Services.AddOptions<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme)
    .Configure<ITicketStore>((options, ticketStore) => options.SessionStore = ticketStore)
    .Configure<TimeProvider>((options, clock) => options.TimeProvider = clock);
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(FrontendPolicies.BillingStaff, policy => policy.RequireAuthenticatedUser()
        .RequireRole(RoleNames.Employee, RoleNames.LotAdmin, RoleNames.AuctionAdmin, RoleNames.Admin))
    .AddPolicy(FrontendPolicies.Customer, policy =>
        policy.RequireAuthenticatedUser().RequireRole(RoleNames.Customer))
    .AddPolicy(FrontendPolicies.Admin, policy =>
        policy.RequireAuthenticatedUser().RequireRole(RoleNames.Admin))
    .AddPolicy(FrontendPolicies.CanViewLots, policy =>
        policy.RequireAuthenticatedUser().RequireClaim(FrontendPolicies.PermissionClaimType, FrontendPolicies.ViewLotsPermission))
    .AddPolicy(FrontendPolicies.CanCreateLot, policy =>
        policy.RequireAuthenticatedUser().RequireClaim(FrontendPolicies.PermissionClaimType, FrontendPolicies.CreateLotPermission))
    .AddPolicy(FrontendPolicies.CanUpdateLot, policy =>
        policy.RequireAuthenticatedUser().RequireClaim(FrontendPolicies.PermissionClaimType, FrontendPolicies.UpdateLotPermission))
    .AddPolicy(FrontendPolicies.CanDeleteLot, policy =>
        policy.RequireAuthenticatedUser().RequireClaim(FrontendPolicies.PermissionClaimType, FrontendPolicies.DeleteLotPermission))
    .AddPolicy(FrontendPolicies.CanViewAuctions, policy =>
        policy.RequireAuthenticatedUser().RequireClaim(FrontendPolicies.PermissionClaimType, FrontendPolicies.ViewAuctionsPermission))
    .AddPolicy(FrontendPolicies.CanCreateAuction, policy =>
        policy.RequireAuthenticatedUser().RequireClaim(FrontendPolicies.PermissionClaimType, FrontendPolicies.CreateAuctionPermission))
    .AddPolicy(FrontendPolicies.CanUpdateAuction, policy =>
        policy.RequireAuthenticatedUser().RequireClaim(FrontendPolicies.PermissionClaimType, FrontendPolicies.UpdateAuctionPermission))
    .AddPolicy(FrontendPolicies.CanDeleteAuction, policy =>
        policy.RequireAuthenticatedUser().RequireClaim(FrontendPolicies.PermissionClaimType, FrontendPolicies.DeleteAuctionPermission))
    .AddPolicy(FrontendPolicies.CanViewEmployees, policy =>
        policy.RequireAuthenticatedUser().RequireClaim(FrontendPolicies.PermissionClaimType, FrontendPolicies.ViewEmployeesPermission));

string apiBaseUrl = builder.Configuration["Api:BaseUrl"]
    ?? throw new InvalidOperationException("Configuration value 'Api:BaseUrl' is required.");

builder.Services.AddHttpClient("BackendApi", client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(20);
});
builder.Services.AddHttpClient("BackendAuthentication", client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(10);
});
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.KnownProxies.Clear();
    options.KnownIPNetworks.Clear();
    foreach (string address in builder.Configuration.GetSection("Authentication:KnownProxies").Get<string[]>() ?? [])
        options.KnownProxies.Add(IPAddress.Parse(address));
    options.ForwardLimit = 1;
    options.ForwardedHeaders = options.KnownProxies.Count == 0 ? ForwardedHeaders.None
        : ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
});
builder.Services.AddScoped(services => new BackendApiClient(
    services.GetRequiredService<IHttpClientFactory>().CreateClient("BackendApi"),
    services.GetRequiredService<BackendTokenAccessor>(),
    services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>(),
    services.GetRequiredService<IHttpContextAccessor>(),
    services.GetRequiredService<FrontendRedirectState>()));
builder.Services.AddScoped<LotService>();
builder.Services.AddScoped<AuctionService>();
builder.Services.AddScoped<EmployeeService>();
builder.Services.AddScoped<CustomerService>();
builder.Services.AddScoped<CustomerAuctionService>();
builder.Services.AddScoped<CustomerAuctionRealtimeService>();
builder.Services.AddScoped<InvoiceService>();

builder.Services.AddScoped<AuctionRealtimeService>();
builder.Services.AddScoped<EmployeeRealtimeService>();

WebApplication app = builder.Build();
app.UseForwardedHeaders();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.UseMiddleware<FrontendRedirectMiddleware>();
app.UseAntiforgery();

app.MapLocalAuthEndpoints();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

await app.RunAsync();
