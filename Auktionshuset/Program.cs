using Auktionshuset.Contracts.Security;
using Auktionshuset.Components;
using Auktionshuset.Security;
using Auktionshuset.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<ServerTicketStore>();
builder.Services.AddSingleton<ITicketStore>(services => services.GetRequiredService<ServerTicketStore>());
builder.Services.AddScoped<AuthenticationStateProvider, SessionRevalidatingAuthenticationStateProvider>();
builder.Services.AddScoped<BackendTokenAccessor>();
builder.Services.AddScoped<FrontendRedirectState>();

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
        options.ExpireTimeSpan = TimeSpan.FromHours(1);
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.Redirect("/login");
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.Redirect("/?access=denied");
            return Task.CompletedTask;
        };
    });
builder.Services.AddOptions<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme)
    .Configure<ITicketStore>((options, ticketStore) => options.SessionStore = ticketStore);
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(FrontendPolicies.Admin, policy =>
        policy.RequireAuthenticatedUser().RequireRole(AdminRoles.SuperAdmin));
    options.AddPolicy(FrontendPolicies.CanViewLots, policy =>
        policy.RequireAuthenticatedUser().RequireAssertion(context => AdminRoles.CanReadLots(context.User)));
    options.AddPolicy(FrontendPolicies.CanCreateLot, policy =>
        policy.RequireAuthenticatedUser().RequireAssertion(context => AdminRoles.CanManageLots(context.User)));
    options.AddPolicy(FrontendPolicies.CanUpdateLot, policy =>
        policy.RequireAuthenticatedUser().RequireAssertion(context => AdminRoles.CanManageLots(context.User)));
    options.AddPolicy(FrontendPolicies.CanDeleteLot, policy =>
        policy.RequireAuthenticatedUser().RequireAssertion(context => AdminRoles.CanManageLots(context.User)));
    options.AddPolicy(FrontendPolicies.CanCreateAuction, policy =>
        policy.RequireAuthenticatedUser().RequireAssertion(context => AdminRoles.CanManageAuctions(context.User)));
});

string apiBaseUrl = builder.Configuration["Api:BaseUrl"]
    ?? throw new InvalidOperationException("Configuration value 'Api:BaseUrl' is required.");

builder.Services.AddHttpClient("BackendApi", client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
});
builder.Services.AddHttpClient("BackendAuthentication", client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
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

builder.Services.AddScoped<AuctionRealtimeService>();
builder.Services.AddScoped<EmployeeRealtimeService>();

WebApplication app = builder.Build();

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
app.UseMiddleware<FrontendRedirectMiddleware>();
app.UseAntiforgery();

app.MapLocalAuthEndpoints();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

public partial class Program { }
