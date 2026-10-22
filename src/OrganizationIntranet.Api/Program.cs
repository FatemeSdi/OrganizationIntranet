using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using OrganizationIntranet.Api.Data;
using OrganizationIntranet.Api.Security;
using OrganizationIntranet.Application.Abstractions;
using OrganizationIntranet.Application.Services;
using OrganizationIntranet.Contracts;
using OrganizationIntranet.Authorization;
using Microsoft.OpenApi.Models;
using OrganizationIntranet.Api.OpenApi;
using Microsoft.AspNetCore.DataProtection;

var builder = WebApplication.CreateBuilder(args);
// User Secrets are read only in Development; say which environment is active so a wrong launch profile is obvious.
static string SecretsDiagnostics(IConfiguration configuration)
{
    // Where this process looked for User Secrets and what it effectively sees: names and lengths only, never values.
    var id = typeof(Program).Assembly.GetCustomAttribute<Microsoft.Extensions.Configuration.UserSecrets.UserSecretsIdAttribute>()?.UserSecretsId;
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
var environmentHint = $" (Environment: {builder.Environment.EnvironmentName}. User Secrets are read only when ASPNETCORE_ENVIRONMENT=Development; start with the 'https' launch profile or tools/Start-Dev.ps1.{(builder.Environment.IsDevelopment() ? SecretsDiagnostics(builder.Configuration) : "")})"; // paths/user details only on a developer machine
var clientKey = builder.Configuration["Api:ClientKey"];
if (string.IsNullOrWhiteSpace(clientKey) || clientKey.Length < 32)
    throw new InvalidOperationException("Configure Api:ClientKey (at least 32 characters) in user secrets or the deployment secret store." + environmentHint);
var adminClientKey = builder.Configuration["Api:AdminClientKey"];
if (string.IsNullOrWhiteSpace(adminClientKey) || adminClientKey.Length < 32 || adminClientKey == clientKey)
    throw new InvalidOperationException("Configure a distinct Api:AdminClientKey (at least 32 characters) shared with Admin only." + environmentHint);
var connection = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Configure ConnectionStrings:DefaultConnection for the API." + environmentHint);
builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlServer(connection));
builder.Services.AddScoped<IIntranetRepository, IntranetRepository>();
builder.Services.AddScoped<IPasswordService, PasswordService>();
builder.Services.AddScoped<IntranetService>();
builder.Services.AddScoped<IApplicationCatalogRepository, ApplicationCatalogRepository>();
builder.Services.AddScoped<ApplicationCatalogService>();
builder.Services.AddScoped<OrganizationCatalogImportService>();
builder.Services.AddScoped<IAccessRequestRepository, AccessRequestRepository>();
builder.Services.AddScoped<AccessRequestService>();
builder.Services.AddScoped<ISupervisionRepository, SupervisionRepository>();
builder.Services.AddScoped<SupervisionService>();
OrganizationIntranet.Api.Integrations.InternalSmsGatewayRegistration.AddInternalSmsGateway(builder.Services);
builder.Services.AddScoped<IAccessRequestSmsSender, OrganizationIntranet.Api.Integrations.AccessRequestSmsSender>();
builder.Services.AddHttpClient("access-request-sms", client => client.Timeout = TimeSpan.FromSeconds(15))
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false });
builder.Services.AddHostedService<OrganizationIntranet.Api.Integrations.AccessRequestSmsWorker>();
builder.Services.AddScoped<INotificationIntegrationRepository, NotificationIntegrationRepository>();
builder.Services.AddScoped<NotificationIntegrationService>();
builder.Services.AddHttpClient<OrganizationIntranet.Api.Integrations.NotificationPullClient>(client => client.Timeout = TimeSpan.FromSeconds(10))
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false });
builder.Services.AddHostedService<OrganizationIntranet.Api.Integrations.NotificationPullWorker>();
builder.Services.AddScoped<IAuthenticationRepository, AuthenticationRepository>();
builder.Services.AddScoped<IAuthenticationProviders, AuthenticationProviders>();
builder.Services.AddScoped<AuthenticationService>();
builder.Services.AddHttpClient("authentication-sms", client => client.Timeout = TimeSpan.FromSeconds(15))
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false });
var authKeysPath = builder.Configuration["Authentication:DataProtectionKeysPath"] ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OrganizationIntranet", "ApiKeys");
var authProtection = builder.Services.AddDataProtection().SetApplicationName("OrganizationIntranet.Api").PersistKeysToFileSystem(new DirectoryInfo(authKeysPath));
if (OperatingSystem.IsWindows()) authProtection.ProtectKeysWithDpapi();
builder.Services.AddScoped<IPublicationRepository, PublicationRepository>();
builder.Services.AddScoped<PublicationService>();
builder.Services.AddControllers();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Organization Intranet API", Version = "v1" });
    options.AddSecurityDefinition("ClientKey", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.ApiKey, In = ParameterLocation.Header, Name = "X-Intranet-Client",
        Description = "Enter Api:ClientKey from the API user secrets. Required for every API request."
    });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http, Scheme = "bearer",
        Description = "Enter accessToken returned by POST /api/account/login, without the Bearer prefix."
    });
    options.AddSecurityDefinition("AdminClient", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.ApiKey, In = ParameterLocation.Header, Name = "X-Intranet-Admin",
        Description = "Reserved for the trusted Admin server. Not available to Portal or browser clients."
    });
    options.AddSecurityDefinition("ApplicationIntegration", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.ApiKey, In = ParameterLocation.Header, Name = "X-Application-Key",
        Description = "Dedicated credential for the source application. Grants notification ingestion only."
    });
    options.OperationFilter<ApiSecurityOperationFilter>();
});
builder.Services.AddAuthentication(BearerTokenDefaults.AuthenticationScheme)
    .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, ApplicationIntegrationHandler>(ApplicationIntegrationHandler.SchemeName, _ => { })
    .AddBearerToken(options =>
{
    options.BearerTokenExpiration = TimeSpan.FromDays(14);
    options.RefreshTokenExpiration = TimeSpan.FromDays(14);
});
builder.Services.AddAuthorization(options => options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
// مجوزها زنده از بانک بررسی می‌شوند (LivePermissions)؛ Policyها با پیشوند Permission: ساخته می‌شوند.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<OrganizationIntranet.Api.Security.LivePermissions>();
builder.Services.AddScoped<IAuthorizationHandler, OrganizationIntranet.Api.Security.LivePermissionAuthorizationHandler>();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("integration", context => RateLimitPartition.GetFixedWindowLimiter(context.User.FindFirst("application_code")?.Value ?? "unknown", _ => new FixedWindowRateLimiterOptions { PermitLimit = 60, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    // همه‌ی بازدیدکنندگان از طریق سرور Portal می‌آیند، پس سقف سخاوتمندانه است و فقط جلوی سیل درخواست مستقیم را می‌گیرد.
    options.AddPolicy("public", context => RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions { PermitLimit = 600, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    // تصاویر عمومی مطالب (کارت‌های اخبار): نشانی نسخه‌دار و کش مرورگر دارند؛ سقف جدا تا سهمیه‌ی داده‌ی عمومی را مصرف نکنند.
    options.AddPolicy("public-media", context => RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions { PermitLimit = 3000, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.AddPolicy("account", context => RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
var app = builder.Build();
if (!app.Environment.IsDevelopment()) app.UseHsts();
app.UseHttpsRedirection();
if (app.Environment.IsDevelopment())
{
    // Documentation is public only in development; API authentication remains unchanged.
    app.UseSwagger();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("v1/swagger.json", "Organization Intranet API v1"));
    app.Use(async (context, next) =>
    {
        if (context.Request.Path == "/" && HttpMethods.IsGet(context.Request.Method))
        {
            context.Response.Redirect("swagger");
            return;
        }
        await next();
    });
}
app.UseRouting();
app.Use(async (context, next) =>
{
    context.Response.Headers.CacheControl = "no-store";
    // Only trusted server-side UI clients may call the API, including anonymous account operations.
    // This preserves the UI CAPTCHA boundary; the application never embeds the key in browser content.
    var supplied = context.Request.Headers["X-Intranet-Client"].ToString();
    if (context.GetEndpoint()?.Metadata.GetMetadata<ApplicationIntegrationAttribute>() is null
        && !CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(supplied)), SHA256.HashData(Encoding.UTF8.GetBytes(clientKey))))
    {
        context.Response.StatusCode = 401; return;
    }
    try { await next(); }
    catch (PublicationConflictException ex)
    {
        context.Response.StatusCode = 409;
        await context.Response.WriteAsJsonAsync(new OperationResult(false, ex.Message));
    }
    catch (AccessRequestConflictException ex)
    {
        context.Response.StatusCode = 409;
        await context.Response.WriteAsJsonAsync(new OperationResult(false, ex.Message));
    }
    catch (Exception ex) when (ex is ArgumentException or KeyNotFoundException or DbUpdateException)
    {
        context.Response.StatusCode = ex is KeyNotFoundException ? 404 : ex is DbUpdateException ? 409 : 400;
        if (ex is DbUpdateException) app.Logger.LogWarning("Database write rejected. TraceId: {TraceId}", context.TraceIdentifier);
        await context.Response.WriteAsJsonAsync(new OperationResult(false, ex is ArgumentException ? ex.Message : ex is KeyNotFoundException ? "رکورد یافت نشد" : "ذخیره‌سازی ناموفق بود؛ داده تکراری یا وابستگی نامعتبر است"));
    }
    catch (Exception ex)
    {
        app.Logger.LogError("API request failed ({ExceptionType}). TraceId: {TraceId}", ex.GetType().Name, context.TraceIdentifier);
        context.Response.StatusCode = 500;
        await context.Response.WriteAsJsonAsync(new OperationResult(false, "خطا در ارتباط با سرویس؛ دوباره تلاش کنید"));
    }
});
app.UseAuthentication();
app.UseMiddleware<AuthenticationSessionMiddleware>();
app.UseAuthorization();
app.UseMiddleware<AdminClientMiddleware>();
app.UseRateLimiter();
app.MapControllers();
app.Run();

public partial class Program { }
