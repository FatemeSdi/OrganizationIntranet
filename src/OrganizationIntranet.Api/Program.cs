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

var builder = WebApplication.CreateBuilder(args);
var clientKey = builder.Configuration["Api:ClientKey"];
if (string.IsNullOrWhiteSpace(clientKey) || clientKey.Length < 32)
    throw new InvalidOperationException("Configure Api:ClientKey (at least 32 characters) in user secrets or the deployment secret store.");
var connection = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Configure ConnectionStrings:DefaultConnection for the API.");
builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlServer(connection));
builder.Services.AddScoped<IIntranetRepository, IntranetRepository>();
builder.Services.AddScoped<IPasswordService, PasswordService>();
builder.Services.AddScoped<IntranetService>();
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
    options.OperationFilter<ApiSecurityOperationFilter>();
});
builder.Services.AddAuthentication(BearerTokenDefaults.AuthenticationScheme).AddBearerToken(options =>
{
    options.BearerTokenExpiration = TimeSpan.FromDays(14);
    options.RefreshTokenExpiration = TimeSpan.FromDays(14);
});
builder.Services.AddAuthorization(options => options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
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
app.Use(async (context, next) =>
{
    context.Response.Headers.CacheControl = "no-store";
    // Only trusted server-side UI clients may call the API, including anonymous account operations.
    // This preserves the UI CAPTCHA boundary; the application never embeds the key in browser content.
    var supplied = context.Request.Headers["X-Intranet-Client"].ToString();
    if (!CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(supplied)), SHA256.HashData(Encoding.UTF8.GetBytes(clientKey))))
    {
        context.Response.StatusCode = 401; return;
    }
    try { await next(); }
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
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapControllers();
app.Run();

public partial class Program { }
