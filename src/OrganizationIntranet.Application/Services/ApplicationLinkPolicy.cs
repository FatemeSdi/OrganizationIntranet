namespace OrganizationIntranet.Application.Services;

/// <summary>سیاست امنیتی لینک‌های بیرونی: مقصد اعلان یا کارت فقط وقتی معتبر است که هم‌مبدأ (طرح، میزبان و پورت) نشانی ثبت‌شده‌ی خود سامانه باشد.</summary>
public static class ApplicationLinkPolicy
{
    /// <summary>مقصد امن را برمی‌گرداند یا null اگر نشانی سامانه معتبر نباشد یا مقصد از مبدأ دیگری، با نام کاربری در آدرس یا بلندتر از ۵۰۰ نویسه باشد. مقصد خالی = خود نشانی سامانه.</summary>
    public static string? Destination(string? applicationUrl, string? targetUrl)
    {
        if (!Uri.TryCreate(applicationUrl, UriKind.Absolute, out var origin) || origin.Scheme is not ("https" or "http") || origin.UserInfo.Length > 0) return null;
        if (!Uri.TryCreate(origin, string.IsNullOrWhiteSpace(targetUrl) ? applicationUrl : targetUrl, out var target)
            || target.Scheme != origin.Scheme || !string.Equals(target.IdnHost, origin.IdnHost, StringComparison.OrdinalIgnoreCase)
            || target.Port != origin.Port || target.UserInfo.Length > 0 || target.AbsoluteUri.Length > 500) return null;
        return target.AbsoluteUri;
    }
}
