using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OrganizationIntranet.Api.Data;

namespace OrganizationIntranet.Api.Security;

/// <summary>احراز هویت سامانه‌های دیگر (دریافت اعلان، تحویل بلیت SSO): هدر X-Application-Key باید با کلیدِ همان سامانه (ApplicationIntegrations:{CODE}:{KeyName}؛ کلید SSO از بانک و به‌صورت هش) یکی باشد. کلید باید دست‌کم ۳۲ نویسه و متفاوت از کلیدهای UI باشد.</summary>
public sealed class ApplicationIntegrationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger,
    UrlEncoder encoder, IConfiguration configuration) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    /// <summary>نام Scheme احراز هویت سامانه‌ها.</summary>
    public const string SchemeName = "ApplicationIntegration";
    /// <summary>کد سامانه را از مسیر می‌خواند، کلید همان کاربرد را (PushKey یا مثلاً SsoKey از [ApplicationIntegration]) از تنظیمات برمی‌دارد و در زمان ثابت با هدر مقایسه می‌کند. موفقیت یعنی Claim application_code.</summary>
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var code = Context.Request.RouteValues["applicationCode"]?.ToString()?.ToUpperInvariant();
        if (code is null || code.Length > 50 || !Regex.IsMatch(code, "^[A-Z0-9_-]+$")) return AuthenticateResult.Fail("Invalid application");
        var keyName = Context.GetEndpoint()?.Metadata.GetMetadata<ApplicationIntegrationAttribute>()?.KeyName ?? "PushKey";
        var supplied = Request.Headers["X-Application-Key"].ToString();
        if (supplied.Length > 500) return AuthenticateResult.Fail("Invalid application credential");
        var suppliedHash = SHA256.HashData(Encoding.UTF8.GetBytes(supplied));
        byte[]? expectedHash;
        if (keyName == "SsoKey")
        {
            // کلید SSO در بانک و فقط به‌صورت هش است (پنل مدیریت)؛ کلید داخلی UI یا کلید اعلان نمی‌تواند جای آن را بگیرد چون هشش هرگز با آن ثبت نمی‌شود.
            var db = Context.RequestServices.GetRequiredService<AppDbContext>();
            expectedHash = await db.Applications.AsNoTracking().Where(a => a.ApplicationCode == code && a.IsActive).Select(a => a.SsoKeyHash).SingleOrDefaultAsync(Context.RequestAborted);
        }
        else
        {
            var expected = configuration[$"ApplicationIntegrations:{code}:{keyName}"];
            expectedHash = string.IsNullOrWhiteSpace(expected) || expected.Length < 32 || expected == configuration["Api:ClientKey"] || expected == configuration["Api:AdminClientKey"]
                ? null : SHA256.HashData(Encoding.UTF8.GetBytes(expected));
        }
        if (expectedHash is null || !CryptographicOperations.FixedTimeEquals(expectedHash, suppliedHash)) return AuthenticateResult.Fail("Invalid application credential");
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("application_code", code)], SchemeName));
        return AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName));
    }
}
