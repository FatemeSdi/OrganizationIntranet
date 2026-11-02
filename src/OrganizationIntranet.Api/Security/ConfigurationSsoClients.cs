using OrganizationIntranet.Application.Abstractions;

namespace OrganizationIntranet.Api.Security;

/// <summary>
/// SSO هر سامانه فقط وقتی فعال است که در تنظیمات API کلید مستقل (<c>ApplicationIntegrations:{CODE}:SsoKey</c>، دست‌کم ۳۲ کاراکتر) و مسیر دریافت
/// بلیت ثبت شده باشد. مسیر یکی از این دو است:
///  - <c>SsoPath</c> (پیشنهادی، مستقل از سیستم): مسیر نسبی صفحه‌ی SSO سامانه مثل <c>Security/Account/Sso</c>؛ آدرس کامل از «نشانی ورود» (BaseUrl) سامانه در پنل
///    ساخته می‌شود، پس میزبان و پورت هر سیستم فقط یک‌جا، در پنل، تنظیم می‌شود و با هر pull/push عوض نمی‌شود.
///  - <c>SsoUrl</c> (اختیاری): آدرس مطلق که اگر باشد بر SsoPath مقدم است.
/// نوشتن فقط BaseUrl در پنل بدون کلید SSO سامانه را فعال نمی‌کند؛ و بلیت بدون کلید خود سامانه (X-Application-Key) قابل مصرف نیست.
/// </summary>
public sealed class ConfigurationSsoClients(IConfiguration configuration) : ISsoClients
{
    public string? LandingUrl(string applicationCode, string? baseUrl = null)
    {
        var code = applicationCode.ToUpperInvariant();
        var key = configuration[$"ApplicationIntegrations:{code}:SsoKey"];
        if (string.IsNullOrWhiteSpace(key) || key.Length < 32 || key == configuration["Api:ClientKey"] || key == configuration["Api:AdminClientKey"]
            || key == configuration[$"ApplicationIntegrations:{code}:PushKey"]) return null;

        if (Uri.TryCreate(configuration[$"ApplicationIntegrations:{code}:SsoUrl"], UriKind.Absolute, out var explicitUri) && IsSafe(explicitUri)) return explicitUri.AbsoluteUri;
        if (!string.IsNullOrWhiteSpace(configuration[$"ApplicationIntegrations:{code}:SsoUrl"])) return null; // SsoUrl نامعتبر به SsoPath برنمی‌گردد

        var rawPath = configuration[$"ApplicationIntegrations:{code}:SsoPath"]?.Trim();
        if (rawPath is not null && rawPath.StartsWith("//")) return null;      // مسیر «//میزبان» نسبی نیست
        var path = rawPath?.TrimStart('/');
        if (string.IsNullOrEmpty(path) || path.Contains("//") || path.Contains('\\') || path.Contains(':') || path.Contains('#') || path.Contains("..")) return null;
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var origin) || !IsSafe(origin)) return null;
        var root = origin.AbsoluteUri;
        root = root[..(root.IndexOfAny(['?', '#']) is var cut and >= 0 ? cut : root.Length)];
        if (!root.EndsWith('/')) root += "/";
        // مسیر نسبی به نشانی ورود چسبانده می‌شود و میزبان هرگز عوض نمی‌شود.
        return Uri.TryCreate(new Uri(root), path, out var combined) && IsSafe(combined) && combined.Authority == origin.Authority ? combined.AbsoluteUri : null;
    }

    private static bool IsSafe(Uri uri) => uri.Scheme is "https" or "http" && string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Fragment);
}
