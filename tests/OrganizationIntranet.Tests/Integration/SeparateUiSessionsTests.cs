extern alias PortalEntry;
extern alias AdminEntry;
using System.Net;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace OrganizationIntranet.Tests;

// نشست Portal و Admin جداست: هرکدام کوکی و کلید رمزگذاری خودش را دارد، پس ورود با یک کاربر در یکی، کاربر دیگری را در دیگری عوض نمی‌کند.
public sealed partial class PortalSmokeTests
{
    [Fact]
    public async Task Portal_and_admin_use_different_cookie_names_and_cannot_read_each_others_session()
    {
        var token = await Login();
        using var portal = Ui<PortalEntry::Program>("Portal");
        using var admin = Ui<AdminEntry::Program>("Admin");
        string CookieName(IServiceProvider services) => services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(CookieAuthenticationDefaults.AuthenticationScheme).Cookie.Name!;
        Assert.Equal("OrganizationIntranet.UI.Portal", CookieName(portal.Services));
        Assert.Equal("OrganizationIntranet.UI.Admin", CookieName(admin.Services));

        // کوکی صادرشده‌ی Admin (با تمام مجوزها) برای Portal ناشناخته است و برعکس؛ هیچ‌کدام دیگری را احراز نمی‌کند.
        var adminCookie = Cookie(admin.Services, token, admin: true);
        var portalCookie = Cookie(portal.Services, token, admin: false);
        using var portalClient = portal.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        portalClient.DefaultRequestHeaders.Add("Cookie", adminCookie);
        var asAnonymous = await portalClient.GetAsync("/Account/Profile");
        Assert.Equal(HttpStatusCode.Redirect, asAnonymous.StatusCode);                                                 // پروفایل Portal: کوکی Admin کافی نیست
        Assert.Contains("/Account/Login", asAnonymous.Headers.Location!.OriginalString);

        using var adminClient = admin.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        adminClient.DefaultRequestHeaders.Add("Cookie", portalCookie);
        var denied = await adminClient.GetAsync("/Admin");
        Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);                                                       // پنل: کوکی Portal کافی نیست
        Assert.Contains("/Account/Login", denied.Headers.Location!.OriginalString);

        // هر دو کوکی کنار هم (مثل یک مرورگر روی localhost): هر UI فقط کوکی خودش را می‌خواند، پس دو کاربر هم‌زمان می‌مانند.
        using var both = admin.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        both.DefaultRequestHeaders.Add("Cookie", portalCookie + "; " + adminCookie);
        Assert.Equal(HttpStatusCode.OK, (await both.GetAsync("/Admin")).StatusCode);                                    // Admin کوکی خودش را برداشت
    }

    [Fact]
    public async Task Each_ui_keeps_its_own_data_protection_purpose()
    {
        using var portal = Ui<PortalEntry::Program>("Portal");
        using var admin = Ui<AdminEntry::Program>("Admin");
        var fromPortal = portal.Services.GetRequiredService<IDataProtectionProvider>().CreateProtector("probe").Protect("secret");
        Assert.ThrowsAny<System.Security.Cryptography.CryptographicException>(() =>
            admin.Services.GetRequiredService<IDataProtectionProvider>().CreateProtector("probe").Unprotect(fromPortal));   // داده‌ی رمزشده‌ی Portal در Admin باز نمی‌شود
        await Task.CompletedTask;
    }
}
