namespace OrganizationIntranet.Application.Services;

/// <summary>
/// آدرس صفحه‌ی دریافت بلیت SSO یک سامانه. آدرس کامل از «نشانی ورود» (BaseUrl) سامانه ساخته می‌شود و مسیر نسبی SsoPath
/// (مثل <c>Security/Account/Sso</c>) به آن می‌چسبد؛ پس میزبان و پورت هر سیستم فقط یک‌جا، در پنل، تنظیم می‌شود و مسیر نمی‌تواند میزبان را عوض کند.
/// </summary>
public static class SsoLanding
{
    /// <summary>آدرس صفحه‌ی SSO؛ null اگر مسیر یا نشانی ورود نامعتبر باشد (مسیر مطلق، «//»، «\»، «:»، «#» یا «..» پذیرفته نمی‌شود).</summary>
    public static string? Url(string? baseUrl, string? ssoPath)
    {
        var rawPath = ssoPath?.Trim();
        if (rawPath is not null && rawPath.StartsWith("//")) return null;      // مسیر «//میزبان» نسبی نیست
        var path = rawPath?.TrimStart('/');
        if (string.IsNullOrEmpty(path) || path.Contains("//") || path.Contains('\\') || path.Contains(':') || path.Contains('#') || path.Contains("..")) return null;
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var origin) || !IsSafe(origin)) return null;
        var root = origin.AbsoluteUri;
        root = root[..(root.IndexOfAny(['?', '#']) is var cut and >= 0 ? cut : root.Length)];
        if (!root.EndsWith('/')) root += "/";
        return Uri.TryCreate(new Uri(root), path, out var combined) && IsSafe(combined) && combined.Authority == origin.Authority ? combined.AbsoluteUri : null;
    }

    /// <summary>SSO فقط وقتی فعال است که هم کلید ثبت شده باشد و هم آدرس درست ساخته شود؛ بلیت بدون کلید خود سامانه قابل مصرف نیست.</summary>
    public static string? Url(string? baseUrl, string? ssoPath, bool hasKey) => hasKey ? Url(baseUrl, ssoPath) : null;

    private static bool IsSafe(Uri uri) => uri.Scheme is "https" or "http" && string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Fragment);
}
