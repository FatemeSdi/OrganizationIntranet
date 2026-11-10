namespace OrganizationIntranet.UI;

// Admin is a server-side BFF. Only its configured HTTPS origin may serve administrative pages.
/// <summary>مرز UI مدیریت: صفحه‌های /Admin فقط روی مبدأ HTTPS تنظیم‌شده (Sites:Admin) سرو می‌شوند و درخواست‌های غیر GET با Origin بیگانه رد می‌شوند.</summary>
public sealed class AdminUiBoundaryMiddleware(RequestDelegate next, IConfiguration configuration)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/Admin"))
        {
            var expected = new Uri(configuration["Sites:Admin"]!).GetLeftPart(UriPartial.Authority);
            var actual = $"{context.Request.Scheme}://{context.Request.Host}";
            if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            { context.Response.StatusCode = StatusCodes.Status400BadRequest; return; }
            // Ports matter: Portal and Admin are separate origins (each also has its own session cookie).
            if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method))
            {
                var origin = context.Request.Headers.Origin.ToString();
                if (!string.IsNullOrEmpty(origin) && !string.Equals(origin, expected, StringComparison.OrdinalIgnoreCase))
                { context.Response.StatusCode = StatusCodes.Status403Forbidden; return; }
            }
        }
        await next(context);
    }
}
