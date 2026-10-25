extern alias PortalEntry;
using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using Catalog = PortalEntry::OrganizationIntranet.Pages.Library.LibraryCatalog;

namespace OrganizationIntranet.Tests;

// قوانین، انتشارات و فرم‌ها: یک دکمه در صفحه‌ی اصلی به صفحه‌ی /Library با تب بخش و جست‌وجو.
public sealed partial class PortalSmokeTests
{
    [Fact]
    public async Task Home_quick_links_have_one_view_all_button_to_the_library_page()
    {
        using var portal = Ui<PortalEntry::Program>("Portal");
        using var anonymous = portal.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var home = WebUtility.HtmlDecode(await anonymous.GetStringAsync("/Portal"));
        var section = home[home.IndexOf("id=\"quicklinks\"", StringComparison.Ordinal)..];
        section = section[..section.IndexOf("</section>", StringComparison.Ordinal)];
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(section, "class=\"ql-all\""));       // یک دکمه برای کل بخش، نه سه تا
        Assert.Contains("class=\"ql-all\" href=\"/Library\"", section);
        Assert.DoesNotContain("ql-dialog", home);
        Assert.DoesNotContain("data-ql-open", home);
        Assert.Contains("href=\"/Library?group=rules&q=" + Uri.EscapeDataString("کنوانسیون‌ها") + "\"", section);                     // هر مورد به نتیجه‌ی خودش
    }

    [Fact]
    public async Task Library_page_lists_every_group_with_tabs_and_search_for_anonymous_visitors()
    {
        using var portal = Ui<PortalEntry::Program>("Portal");
        using var anonymous = portal.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });

        var all = await anonymous.GetAsync("/Library");
        Assert.Equal(HttpStatusCode.OK, all.StatusCode);
        var html = WebUtility.HtmlDecode(await all.Content.ReadAsStringAsync());
        Assert.Contains("pp-site", html);                                                             // پوسته‌ی پورتال
        foreach (var g in Catalog.Groups) { Assert.Contains($"id=\"lib-{g.Id}\"", html); Assert.Contains($"href=\"/Library?group={g.Id}\"", html); }
        Assert.Contains(Catalog.Fa(Catalog.Groups.Sum(g => g.Items.Count)) + " مورد", html);

        var forms = WebUtility.HtmlDecode(await anonymous.GetStringAsync("/Library?group=forms"));
        Assert.Contains("id=\"lib-forms\"", forms);
        Assert.DoesNotContain("id=\"lib-rules\"", forms);
        Assert.Contains("aria-current=\"page\"", forms);

        var search = WebUtility.HtmlDecode(await anonymous.GetStringAsync("/Library?q=" + Uri.EscapeDataString("راهنماي")));   // ی عربی
        Assert.Contains("راهنمای سیستم مدیریت دانش", search);
        Assert.DoesNotContain("کنوانسیون‌ها</b>", search);
        Assert.Contains("پاک‌کردن فیلتر", search);

        Assert.Contains("موردی مطابق جست‌وجوی شما یافت نشد", WebUtility.HtmlDecode(await anonymous.GetStringAsync("/Library?q=zzzz")));
        Assert.Equal(HttpStatusCode.OK, (await anonymous.GetAsync("/Library?group=unknown&q=" + new string('a', 500))).StatusCode);   // ورودی نامعتبر = پیش‌فرض
        Assert.DoesNotContain("value=\"<script>\"", await anonymous.GetStringAsync("/Library?q=%3Cscript%3E"));   // جست‌وجو encode می‌شود
    }

    [Fact]
    public async Task Library_all_tab_previews_each_group_and_links_to_the_full_group()
    {
        using var portal = Ui<PortalEntry::Program>("Portal");
        using var anonymous = portal.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var rules = Catalog.Find("rules")!;
        var hidden = rules.Items[PortalEntry::OrganizationIntranet.Pages.Library.IndexModel.PreviewLimit].Title;   // اولین مورد بیرون از پیش‌نمایش

        var all = WebUtility.HtmlDecode(await anonymous.GetStringAsync("/Library"));
        Assert.DoesNotContain($"<b>{hidden}</b>", all);                                                      // تب «همه» شلوغ نمی‌شود
        Assert.Contains($"مشاهده‌ی همه‌ی {Catalog.Fa(rules.Items.Count)} مورد «{rules.Title}»", all);
        Assert.Contains("class=\"pp-lib-more\"", all);

        var group = WebUtility.HtmlDecode(await anonymous.GetStringAsync("/Library?group=rules"));
        Assert.Contains($"<b>{hidden}</b>", group);                                                          // تب بخش: همه‌ی موارد
        Assert.DoesNotContain("class=\"pp-lib-more\"", group);

        var search = WebUtility.HtmlDecode(await anonymous.GetStringAsync("/Library?q=" + Uri.EscapeDataString(hidden)));
        Assert.Contains($"<b>{hidden}</b>", search);                                                         // جست‌وجو: همه‌ی نتیجه‌ها
        Assert.DoesNotContain("class=\"pp-lib-more\"", search);
    }

    [Theory]
    [InlineData("دستورالعمل", "دستورالعمل‌ها", true)]
    [InlineData("راهنمای‌کاربری", "راهنمای کاربری امور بندری", true)]   // نیم‌فاصله = فاصله
    [InlineData("ISMS", "بیانیه خط‌مشی ISMS", true)]
    [InlineData("isms", "بیانیه خط‌مشی ISMS", true)]
    [InlineData("بودجه", "دستورالعمل‌ها", false)]
    public void Library_search_normalizes_persian_text(string search, string title, bool expected) =>
        Assert.Equal(expected, Catalog.Matches(new(title, 1), search));
}
