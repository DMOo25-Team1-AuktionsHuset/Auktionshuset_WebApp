namespace Auktionshuset.Security;

public sealed class FrontendRedirectMiddleware(RequestDelegate next)
{
    public const string RedirectItemKey = "Auktionshuset.Redirect";

    public async Task InvokeAsync(HttpContext context)
    {
        if (!ShouldBuffer(context))
        {
            await next(context);
            return;
        }

        Stream originalBody = context.Response.Body;
        await using var bufferedBody = new MemoryStream();
        context.Response.Body = bufferedBody;
        try
        {
            await next(context);
        }
        finally
        {
            context.Response.Body = originalBody;
        }

        string? location = context.Items.TryGetValue(RedirectItemKey, out object? value)
            ? value as string
            : context.RequestServices.GetService<FrontendRedirectState>()?.Location;
        if (location is not null)
        {
            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status302Found;
            context.Response.Headers.Location = location;
            return;
        }

        bufferedBody.Position = 0;
        await bufferedBody.CopyToAsync(originalBody, context.RequestAborted);
    }

    private static bool ShouldBuffer(HttpContext context) =>
        HttpMethods.IsGet(context.Request.Method)
        && (context.Request.Path == "/"
            || context.Request.Path == "/login"
            || context.Request.Path.StartsWithSegments("/warehouse")
            || context.Request.Path.StartsWithSegments("/admin")
            || context.Request.Path.StartsWithSegments("/not-found"));
}
