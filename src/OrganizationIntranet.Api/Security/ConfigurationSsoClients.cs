using OrganizationIntranet.Application.Abstractions;

namespace OrganizationIntranet.Api.Security;

/// <summary>
/// SSO هر سامانه فقط وقتی فعال است که در تنظیمات امن API هم آدرس دریافت بلیت (<c>ApplicationIntegrations:{CODE}:SsoUrl</c>) و هم کلید
/// مستقل (<c>ApplicationIntegrations:{CODE}:SsoKey</c>، دست‌کم ۳۲ کاراکتر) ثبت شده باشد؛ نوشتن آدرس در بانک یا پنل کافی نیست.
/// </summary>
public sealed class ConfigurationSsoClients(IConfiguration configuration) : ISsoClients
{
    public string? LandingUrl(string applicationCode)
    {
        var code = applicationCode.ToUpperInvariant();
        var key = configuration[$"ApplicationIntegrations:{code}:SsoKey"];
        if (string.IsNullOrWhiteSpace(key) || key.Length < 32 || key == configuration["Api:ClientKey"] || key == configuration["Api:AdminClientKey"]
            || key == configuration[$"ApplicationIntegrations:{code}:PushKey"]) return null;
        return Uri.TryCreate(configuration[$"ApplicationIntegrations:{code}:SsoUrl"], UriKind.Absolute, out var uri)
            && uri.Scheme is "https" or "http" && string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Fragment)
            ? uri.AbsoluteUri : null;
    }
}
