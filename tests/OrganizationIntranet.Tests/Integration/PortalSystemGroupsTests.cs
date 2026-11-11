extern alias PortalEntry;
extern alias AdminEntry;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace OrganizationIntranet.Tests;

// صفحه‌ی اصلی پرتال برای کاربر واردشده: سامانه‌ها در دو گروه جدا (عمومی و اختصاصی من) با نشان «ورود یکپارچه»؛ و فرم ویرایش سامانه با انتخاب‌گر کارتی حالت دسترسی.
public sealed partial class PortalSmokeTests
{
    private static string Group(string html, string name)
    {
        var start = html.IndexOf($"data-sys-group=\"{name}\"", StringComparison.Ordinal);
        if (start < 0) return "";
        var next = html.IndexOf("data-sys-group=", start + 10, StringComparison.Ordinal);
        return html[start..(next < 0 ? html.Length : next)];
    }

    [Fact]
    public async Task Signed_in_portal_separates_public_systems_from_my_private_systems_and_marks_single_sign_on()
    {
        SeedCardApplication("GRPPUB", true);                        // عمومی، بدون SSO
        var (ssoApp, _) = SeedSsoApplications();                    // اختصاصی (تخصیص مستقیم) با SSO
        var token = await Login("member");
        using var portal = Ui<PortalEntry::Program>("Portal");
        using var browser = portal.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        browser.DefaultRequestHeaders.Add("Cookie", Cookie(portal.Services, token, false));

        var home = WebUtility.HtmlDecode(await browser.GetStringAsync("/Portal"));
        var publicGroup = Group(home, "public");
        var privateGroup = Group(home, "private");
        Assert.NotEmpty(publicGroup);
        Assert.Contains("سامانه‌های عمومی", publicGroup);
        Assert.Contains("data-name=\"سامانه GRPPUB", publicGroup);
        Assert.DoesNotContain("data-name=\"تسک", publicGroup);                 // سامانه‌ی اختصاصی در گروه عمومی نیست
        Assert.DoesNotContain("sys-sso", publicGroup);                          // سامانه‌ی عمومی بدون SSO نشان ندارد

        Assert.Contains("سامانه‌های اختصاصی من", privateGroup);
        Assert.Contains($"href=\"/Sso/Open?app={ssoApp}\"", privateGroup);
        Assert.Contains("data-name=\"تسک", privateGroup);
        Assert.Contains("class=\"sys-sso\"", privateGroup);                     // نشان «ورود یکپارچه» روی کارت دارای SSO
        Assert.Contains("ورود یکپارچه", privateGroup);
        Assert.Contains("request-access-card", privateGroup);                   // کارت «درخواست دسترسی جدید» کنار سامانه‌های اختصاصی
        Assert.DoesNotContain("data-name=\"سامانه GRPPUB", privateGroup);
        Assert.True(home.IndexOf("data-sys-group=\"public\"", StringComparison.Ordinal) < home.IndexOf("data-sys-group=\"private\"", StringComparison.Ordinal)); // ترتیب: عمومی، بعد اختصاصی
        Assert.Contains("#systems [data-sys-group]", home);                     // جست‌وجو گروه بدون نتیجه را پنهان می‌کند
    }

    [Fact]
    public async Task Signed_in_portal_without_public_systems_shows_only_the_private_group_and_anonymous_visitors_keep_one_grid()
    {
        var token = await Login("member");
        using var portal = Ui<PortalEntry::Program>("Portal");
        using var browser = portal.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        browser.DefaultRequestHeaders.Add("Cookie", Cookie(portal.Services, token, false));
        var home = await browser.GetStringAsync("/Portal");
        Assert.DoesNotContain("data-sys-group=\"public\"", home);               // گروه خالی نمایش داده نمی‌شود
        Assert.Contains("data-sys-group=\"private\"", home);                    // ولی بخش اختصاصی و کارت درخواست همیشه هست
        Assert.Contains("request-access-card", home);

        using var visitor = portal.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var anonymous = await visitor.GetStringAsync("/Portal");
        Assert.DoesNotContain("data-sys-group=", anonymous);                    // بازدیدکننده‌ی ناشناس همان یک شبکه‌ی عمومی را می‌بیند
        Assert.Contains("id=\"systemsGrid\"", anonymous);
    }

    [Fact]
    public async Task Admin_application_form_offers_the_three_access_modes_as_selectable_cards_with_a_searchable_unit_panel()
    {
        var (admin, browser, _, _, _) = await AdminBrowserAsync();
        using (admin) using (browser)
        {
            var html = WebUtility.HtmlDecode(await browser.GetStringAsync("/Admin/Applications/Edit?id=1"));
            Assert.Equal(3, Regex.Matches(html, "class=\"access-mode\"").Count);
            var modeInputs = Regex.Matches(html, "<input[^>]*>").Select(m => m.Value).Where(t => t.Contains("name=\"Input.AccessMode\"")).ToList();
            foreach (var value in new[] { "Request", "Public", "Unit" }) Assert.Single(modeInputs, t => t.Contains($"value=\"{value}\""));
            foreach (var id in new[] { "mode-request", "mode-public", "mode-unit" }) Assert.Contains($"id=\"{id}\"", html);
            foreach (var title in new[] { "با تخصیص یا درخواست", "عمومی", "بر اساس چارت سازمانی" }) Assert.Contains($"<b>{title}</b>", html);
            Assert.Single(modeInputs, t => t.Contains("checked"));                 // فقط یک حالت انتخاب است
            Assert.Contains("id=\"unit-access\" class=\"unit-access\" hidden", html);                            // پنل واحدها فقط در حالت چارت باز می‌شود
            Assert.DoesNotContain("class=\"form-check-label\" for=\"mode-", html);                              // رادیوی ساده‌ی قبلی نیست
        }
    }
}
