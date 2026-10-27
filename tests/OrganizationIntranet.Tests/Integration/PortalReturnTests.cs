extern alias PortalEntry;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace OrganizationIntranet.Tests;

// کارت سامانه در تب جدید باز می‌شود و تب پورتال می‌ماند؛ به همین دلیل سامانه‌ها دکمه‌ی «بازگشت به پورتال» لازم ندارند (نیاز به هیچ تغییری در آن‌ها نیست).
public sealed partial class PortalSmokeTests
{
    [Fact]
    public async Task System_cards_open_in_a_new_tab_without_giving_the_application_access_to_the_portal_window()
    {
        var (app, _) = SeedSsoApplications();
        var token = await Login("member");
        using var portal = Ui<PortalEntry::Program>("Portal");
        using var browser = portal.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        browser.DefaultRequestHeaders.Add("Cookie", Cookie(portal.Services, token, false));

        var home = WebUtility.HtmlDecode(await browser.GetStringAsync("/Portal"));
        Assert.Contains($"<a class=\"sys-card\" href=\"/Sso/Open?app={app}\" target=\"_blank\" rel=\"noopener\"", home);
        Assert.DoesNotContain("href=\"https://tasktrack.example/\"", home);
    }
}
