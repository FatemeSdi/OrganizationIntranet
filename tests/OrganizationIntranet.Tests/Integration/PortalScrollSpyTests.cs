extern alias PortalEntry;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace OrganizationIntranet.Tests;

// هدر صفحه‌ی اصلی پورتال: ترتیب «خانه، اطلاعیه‌ها، سامانه‌ها، مقررات و فرم‌ها» مثل ترتیب بخش‌های صفحه، و هایلایت با اسکرول (portal-scrollspy.js).
public sealed partial class PortalSmokeTests
{
    [Fact]
    public async Task Home_header_lists_news_before_systems_in_page_order_and_loads_the_scroll_spy()
    {
        using var portal = Ui<PortalEntry::Program>("Portal");
        using var anonymous = portal.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var html = WebUtility.HtmlDecode(await anonymous.GetStringAsync("/Portal"));

        var nav = Regex.Match(html, "<nav class=\"main\">.*?</nav>", RegexOptions.Singleline).Value;
        var order = new[] { ">خانه<", ">اطلاعیه‌ها<", ">سامانه‌ها<", ">مقررات و فرم‌ها<" }.Select(t => nav.IndexOf(t, StringComparison.Ordinal)).ToArray();
        Assert.All(order, i => Assert.True(i > 0));
        Assert.Equal(order.OrderBy(i => i), order);                                                       // ترتیب هدر
        Assert.Contains("<a href=\"/Library\" data-spy-target=\"#quicklinks\">", nav);                      // بخش قوانین و فرم‌ها هم هایلایت می‌شود

        var sections = new[] { "id=\"news\"", "id=\"systems\"", "id=\"quicklinks\"" }.Select(t => html.IndexOf(t, StringComparison.Ordinal)).ToArray();
        Assert.All(sections, i => Assert.True(i > 0));
        Assert.Equal(sections.OrderBy(i => i), sections);                                                  // همان ترتیب در خود صفحه
        Assert.Contains("/js/portal-scrollspy.js", html);
        Assert.Contains("scroll-margin-top", html);
    }

    [Fact]
    public async Task Scroll_spy_script_activates_the_section_under_the_header_and_home_on_top()
    {
        using var portal = Ui<PortalEntry::Program>("Portal");
        using var anonymous = portal.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var response = await anonymous.GetAsync("/js/portal-scrollspy.js");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var js = await response.Content.ReadAsStringAsync();
        Assert.Contains("querySelector('nav.main')", js);
        Assert.Contains("data-spy-target", js);                                                             // لینک /Library به بخش quicklinks وصل می‌شود
        Assert.Contains("getBoundingClientRect().top <= line", js);                                         // بخشی که به زیر هدر رسیده
        Assert.Contains("atBottom", js);                                                                    // ته صفحه آخرین بخش
        Assert.Contains("aria-current", js);
        Assert.Contains("{ passive: true }", js);
        Assert.DoesNotContain("requestAnimationFrame(update", js);                                          // rAF در تب مخفی اجرا نمی‌شود
        Assert.Contains("function top(el)", js);                                                            // ترتیب بخش‌ها از جای واقعی، نه offsetTop
    }
}
