using DNTCaptcha.Core;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Authentication;
using OrganizationIntranet.Contracts;
using System.Reflection;
using System.Security.Claims;

namespace OrganizationIntranet.UI;

public static class UiHost
{
    /// <summary>Where this process looked for User Secrets and what it effectively sees: names and lengths only, never values.</summary>
    private static string SecretsDiagnostics(string applicationName, IConfiguration configuration)
    {
        string? id = null;
        try { id = System.Reflection.Assembly.Load(new System.Reflection.AssemblyName(applicationName)).GetCustomAttribute<Microsoft.Extensions.Configuration.UserSecrets.UserSecretsIdAttribute>()?.UserSecretsId; } catch (Exception) { }
        var file = id is null ? null : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft", "UserSecrets", id, "secrets.json");
        var exists = file is not null && File.Exists(file);
        var keys = "n/a";
        if (exists)
        {
            try { using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(file!)); keys = string.Join(", ", document.RootElement.EnumerateObject().Select(p => $"{p.Name}(len={p.Value.ToString().Length})")); }
            catch (Exception) { keys = "unreadable"; }
        }
        var client = configuration["Api:ClientKey"]; var admin = configuration["Api:AdminClientKey"];
        return $" Secrets: windowsUser={Environment.UserName}, id={id ?? "<none>"}, file={file ?? "<none>"}, exists={exists}, keysInFile=[{keys}]; effective ClientKey len={client?.Length ?? 0}, AdminClientKey len={admin?.Length ?? 0}, equal={(client is not null && client == admin)}.";
    }

    public static WebApplication Create(string[] args, string assemblyName)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = args, ApplicationName = assemblyName, WebRootPath = Path.Combine(AppContext.BaseDirectory, "wwwroot") });
        var name = builder.Configuration["Ui:Name"] ?? throw new InvalidOperationException("Configure Ui:Name.");
        // User Secrets are read only in Development; say which environment is active so a wrong launch profile is obvious.
        var hint = $" (Environment: {builder.Environment.EnvironmentName}. User Secrets are read only when ASPNETCORE_ENVIRONMENT=Development; start with the 'https' launch profile or tools/Start-Dev.ps1.{(builder.Environment.IsDevelopment() ? SecretsDiagnostics(assemblyName, builder.Configuration) : "")})"; // paths/user details only on a developer machine
        var key = builder.Configuration["Api:ClientKey"];
        if (string.IsNullOrWhiteSpace(key) || key.Length < 32) throw new InvalidOperationException("Configure Api:ClientKey in user secrets or the deployment secret store." + hint);
        if (name == "Admin")
        {
            var adminKey = builder.Configuration["Api:AdminClientKey"];
            if (string.IsNullOrWhiteSpace(adminKey) || adminKey.Length < 32 || adminKey == key)
                throw new InvalidOperationException("Configure a distinct Api:AdminClientKey for Admin and API only." + hint);
            if (!Uri.TryCreate(builder.Configuration["Sites:Admin"], UriKind.Absolute, out var adminOrigin)
                || adminOrigin.Scheme != "https" || adminOrigin.AbsolutePath != "/" || !string.IsNullOrEmpty(adminOrigin.Query)
                || !string.IsNullOrEmpty(adminOrigin.UserInfo) || !string.IsNullOrEmpty(adminOrigin.Fragment))
                throw new InvalidOperationException("Sites:Admin must be the Admin HTTPS origin, including its port.");
        }
        var apiUrl = new Uri(builder.Configuration["Api:BaseUrl"] ?? throw new InvalidOperationException("Configure Api:BaseUrl."));
        if (apiUrl.Scheme != "https") throw new InvalidOperationException("Api:BaseUrl must use HTTPS.");
        builder.Services.AddRazorPages(options =>
        {
            if (name == "Admin") options.Conventions.AuthorizeFolder("/Admin", "Admin");
            options.Conventions.AuthorizeFolder("/Portal");
            // صفحه‌ی اصلی پرتال برای بازدیدکننده‌ی ناشناس باز است و فقط داده‌ی عمومی نشان می‌دهد؛ دسترسی داده در API کنترل می‌شود، نه در UI.
            if (name == "Portal") options.Conventions.AllowAnonymousToPage("/Portal/Index");
        }).AddMvcOptions(options =>
        {
            options.Filters.Add<ApiPageExceptionFilter>();
            options.Filters.Add<LoginAntiforgeryResultFilter>();
        });
        builder.Services.AddControllers(); // DNTCaptcha image endpoint.
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddHttpClient<ApiClient>(client =>
        {
            client.BaseAddress = apiUrl;
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("X-Intranet-Client", key);
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false });
        var keysPath = builder.Configuration["Ui:DataProtectionKeysPath"] ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OrganizationIntranet", "UIKeys");
        builder.Services.AddDataProtection().SetApplicationName("OrganizationIntranet.UI").PersistKeysToFileSystem(new DirectoryInfo(keysPath));
        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
        {
            options.Cookie.Name = "OrganizationIntranet.UI";
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.Domain = builder.Configuration["Ui:CookieDomain"];
            options.LoginPath = "/Account/Login";
            options.AccessDeniedPath = "/Account/AccessDenied";
            options.SlidingExpiration = true;
            options.Events.OnValidatePrincipal = async context =>
            {
                var now = DateTimeOffset.UtcNow;
                if (context.Properties.IssuedUtc is not { } issued || context.Properties.ExpiresUtc is not { } expires || now - issued <= expires - now) return;
                var refreshToken = context.Principal?.FindFirst("api_refresh")?.Value;
                if (string.IsNullOrEmpty(refreshToken))
                {
                    context.RejectPrincipal();
                    await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                    return;
                }
                try
                {
                    var client = context.HttpContext.RequestServices.GetRequiredService<ApiClient>();
                    var token = await client.PostAsync<TokenResponse>("api/account/refresh", new RefreshRequest(refreshToken));
                    var identity = new ClaimsIdentity(context.Principal!.Claims.Where(c => c.Type is not "api_token" and not "api_refresh"), CookieAuthenticationDefaults.AuthenticationScheme);
                    identity.AddClaim(new Claim("api_token", token.AccessToken));
                    identity.AddClaim(new Claim("api_refresh", token.RefreshToken));
                    context.ReplacePrincipal(new ClaimsPrincipal(identity));
                    context.Properties.IssuedUtc = now;
                    context.Properties.ExpiresUtc = now.AddSeconds(token.ExpiresIn);
                    context.ShouldRenew = true;
                }
                catch (ApiException)
                {
                    // Never extend a UI session beyond the API token when renewal fails.
                    context.RejectPrincipal();
                    await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                }
            };
        });
        builder.Services.AddAuthorization(options => options.AddPolicy("Admin", policy => policy.RequireRole("ADMIN")));
        builder.Services.AddDNTCaptcha(options => options.UseCookieStorageProvider().ShowThousandsSeparators(false).WithEncryptionKey(builder.Configuration["DNTCaptcha:EncryptionKey"] ?? key));
        var app = builder.Build();
        if (!app.Environment.IsDevelopment()) { app.UseExceptionHandler("/Error"); app.UseHsts(); }
        app.UseHttpsRedirection();
        if (name == "Admin") app.UseMiddleware<AdminUiBoundaryMiddleware>();
        app.UseStaticFiles();
        app.Use(async (context, next) =>
        {
            context.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
            context.Response.Headers.Pragma = "no-cache";
            context.Response.Headers.Expires = "-1";
            await next();
        });
        app.UseRouting(); app.UseAuthentication(); app.UseAuthorization();
        // پرتال کارکنان صفحه‌ی اصلی عمومی دارد؛ پنل مدیریت همیشه با ورود شروع می‌شود.
        app.MapGet("/", () => Results.Redirect(name == "Portal" ? "/Portal" : "/Account/Login"));
        app.MapGet("/Account/AccessDenied", () => Results.Content("دسترسی مجاز نیست", "text/plain; charset=utf-8", statusCode: 403));
        app.MapControllers(); app.MapRazorPages();
        return app;
    }
}
