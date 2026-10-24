extern alias PortalEntry;
extern alias AdminEntry;
using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace OrganizationIntranet.Tests;

// صفحه‌های خطای ۴۰۳/۴۰۴/۵۰۰ هم‌تم هر پرتال (UiHost: UseStatusCodePagesWithReExecute و مسیر /Account/AccessDenied) و توست پیام‌های ورود پورتال.
public sealed partial class PortalSmokeTests
{
    private static readonly WebApplicationFactoryClientOptions NoRedirect = new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false };

    [Fact]
    public async Task Portal_error_pages_use_the_portal_theme_without_calling_the_api()
    {
        using var portal = Ui<PortalEntry::Program>("Portal");
        using var anonymous = portal.CreateClient(NoRedirect);

        var missing = await anonymous.GetAsync("/no-such-page");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        var html = WebUtility.HtmlDecode(await missing.Content.ReadAsStringAsync());
        Assert.Contains("class=\"pe-page\"", html);
        Assert.Contains("صفحه پیدا نشد", html);
        Assert.Contains("href=\"/Portal\"", html);
        Assert.DoesNotContain("pp-site", html);                                     // بدون هدر پوسته (که API را صدا می‌زند)

        var token = await Login("member");
        using var signedIn = portal.CreateClient(NoRedirect);
        signedIn.DefaultRequestHeaders.Add("Cookie", Cookie(portal.Services, token, false));
        var denied = await signedIn.GetAsync("/Account/AccessDenied");
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        var deniedHtml = WebUtility.HtmlDecode(await denied.Content.ReadAsStringAsync());
        Assert.Contains("عدم دسترسی", deniedHtml);
        Assert.Contains("href=\"/AccessRequests\"", deniedHtml);                     // کاربر واردشده می‌تواند درخواست دسترسی بدهد
        Assert.DoesNotContain("دسترسی مجاز نیست", deniedHtml);                       // متن ساده‌ی قبلی
    }

    [Fact]
    public async Task Admin_error_pages_use_the_admin_theme_for_missing_pages_access_denied_and_not_found_results()
    {
        using var admin = Ui<AdminEntry::Program>("Admin");
        using var anonymous = admin.CreateClient(NoRedirect);
        var missing = await anonymous.GetAsync("/no-such-page");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        var html = WebUtility.HtmlDecode(await missing.Content.ReadAsStringAsync());
        Assert.Contains("ae-card", html);
        Assert.Contains("صفحه پیدا نشد", html);
        Assert.Contains("href=\"/Account/Login\"", html);                           // ناشناس: ورود به پنل

        var token = await Login();
        using var browser = admin.CreateClient(NoRedirect);
        browser.DefaultRequestHeaders.Add("Cookie", Cookie(admin.Services, token, true));
        var notFound = await browser.GetAsync("/Admin/Roles/Edit?id=99999");          // NotFound() صفحه هم هم‌تم است
        Assert.Equal(HttpStatusCode.NotFound, notFound.StatusCode);
        var notFoundHtml = WebUtility.HtmlDecode(await notFound.Content.ReadAsStringAsync());
        Assert.Contains("صفحه پیدا نشد", notFoundHtml);
        Assert.Contains("href=\"/Admin\"", notFoundHtml);                            // واردشده: بازگشت به داشبورد

        var denied = await browser.GetAsync("/Account/AccessDenied");
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        var deniedHtml = WebUtility.HtmlDecode(await denied.Content.ReadAsStringAsync());
        Assert.Contains("عدم دسترسی", deniedHtml);
        Assert.Contains("href=\"/Account/Logout\"", deniedHtml);
    }

    [Theory]
    [InlineData(null, true, false, 200, 403)]   // مسیر AccessDenied
    [InlineData(null, false, true, 200, 500)]   // خطای پردازش‌نشده
    [InlineData(404, false, false, 404, 404)]   // re-execute
    [InlineData(null, false, false, 405, 405)]
    [InlineData(999, false, false, 200, 404)]   // کد نامعتبر از نشانی
    [InlineData(200, false, false, 200, 404)]
    [InlineData(null, false, false, 200, 500)]
    public void Error_page_status_is_resolved_safely(int? route, bool accessDenied, bool exception, int current, int expected)
    {
        Assert.Equal(expected, PortalEntry::OrganizationIntranet.UI.ErrorPageContent.Resolve(route, accessDenied, exception, current));
        Assert.False(string.IsNullOrWhiteSpace(PortalEntry::OrganizationIntranet.UI.ErrorPageContent.For(expected).Title));
    }

    [Fact]
    public async Task Portal_login_messages_are_portal_toasts_with_a_no_script_fallback()
    {
        using var portal = Ui<PortalEntry::Program>("Portal");
        using var ui = portal.CreateClient(NoRedirect);
        var html = WebUtility.HtmlDecode(await ui.GetStringAsync("/Account/Login?loggedOut=true"));
        Assert.Contains("/js/portal-toast.js", html);
        Assert.Contains("/css/portal-toast.css", html);
        Assert.Contains("<div hidden data-toast=\"success\">با موفقیت از سیستم خارج شدید</div>", html);
        Assert.Contains("<noscript>", html);
        Assert.DoesNotContain("showToast('با موفقیت", html);                        // توست تمپلیت قبلی
        Assert.Contains("window.portalToast", await ui.GetStringAsync("/js/login-form.js")); // خطاهای فرم ورود هم توست پورتال
    }
}
