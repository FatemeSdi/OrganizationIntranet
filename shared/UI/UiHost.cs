using DNTCaptcha.Core;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Authentication;
using OrganizationIntranet.Contracts;
using System.Security.Claims;

namespace OrganizationIntranet.UI;

public static class UiHost
{
    public static WebApplication Create(string[] args, string assemblyName)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = args, ApplicationName = assemblyName, WebRootPath = Path.Combine(AppContext.BaseDirectory, "wwwroot") });
        var name = builder.Configuration["Ui:Name"] ?? throw new InvalidOperationException("Configure Ui:Name.");
        var key = builder.Configuration["Api:ClientKey"];
        if (string.IsNullOrWhiteSpace(key) || key.Length < 32) throw new InvalidOperationException("Configure Api:ClientKey in user secrets or the deployment secret store.");
        var apiUrl = new Uri(builder.Configuration["Api:BaseUrl"] ?? throw new InvalidOperationException("Configure Api:BaseUrl."));
        if (apiUrl.Scheme != "https") throw new InvalidOperationException("Api:BaseUrl must use HTTPS.");
        builder.Services.AddRazorPages(options =>
        {
            if (name == "Admin") options.Conventions.AuthorizeFolder("/Admin", "Admin");
            options.Conventions.AuthorizeFolder("/Portal");
        }).AddMvcOptions(options => options.Filters.Add<ApiPageExceptionFilter>());
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
        app.UseStaticFiles();
        app.Use(async (context, next) =>
        {
            context.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
            context.Response.Headers.Pragma = "no-cache";
            context.Response.Headers.Expires = "-1";
            await next();
        });
        app.UseRouting(); app.UseAuthentication(); app.UseAuthorization();
        app.MapGet("/", () => Results.Redirect("/Account/Login"));
        app.MapGet("/Account/AccessDenied", () => Results.Content("دسترسی مجاز نیست", "text/plain; charset=utf-8", statusCode: 403));
        app.MapControllers(); app.MapRazorPages();
        return app;
    }
}
