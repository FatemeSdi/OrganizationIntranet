extern alias AdminEntry;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace OrganizationIntranet.Tests;

// پیام نتیجه‌ی عملیات در پنل مدیریت باید toast تمپلیت yzen باشد (app-toast.js)، نه کادر ثابت alert بوت‌استرپ.
public sealed partial class PortalSmokeTests
{
    [Fact]
    public async Task Admin_operation_results_are_rendered_as_toasts_with_a_cache_busted_script()
    {
        var token = await Login();
        using var admin = Ui<AdminEntry::Program>("Admin");
        var options = new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false };
        using var browser = admin.CreateClient(options);
        var auth = Cookie(admin.Services, token, true);
        browser.DefaultRequestHeaders.Add("Cookie", auth);
        var values = new Dictionary<string, string> { ["Input.Title"] = "مطلب toast", ["Input.Summary"] = "خلاصه", ["Input.Body"] = "متن", ["Input.Kind"] = "News", ["Input.IsPublished"] = "true",
            ["__RequestVerificationToken"] = await Antiforgery(browser, "/Admin/Publications/Edit") };
        var saved = await browser.PostAsync("/Admin/Publications/Edit", new FormUrlEncodedContent(values));
        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);

        // پیام موفقیت در TempData (کوکی) است؛ آن را همراه کوکی ورود به صفحه‌ی بعد می‌فرستیم، مثل مرورگر.
        var tempData = saved.Headers.GetValues("Set-Cookie").Select(c => c.Split(';')[0]).Single(c => c.StartsWith(".AspNetCore.Mvc.CookieTempDataProvider", StringComparison.Ordinal));
        using var next = admin.CreateClient(options);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/Admin/Publications");
        request.Headers.Add("Cookie", auth + "; " + tempData);
        var html = await (await next.SendAsync(request)).Content.ReadAsStringAsync();

        Assert.Contains("data-toast=\"success\"", html);
        Assert.Contains("محتوا برای کاربران واردشده منتشر شد", WebUtility.HtmlDecode(html));
        Assert.DoesNotContain("alert alert-success", html);
        Assert.Matches(new Regex("app-toast\\.js\\?v="), html); // asp-append-version: مرورگر نسخه‌ی قدیمی را از کش نشان نمی‌دهد

        // اسکریپت toast هم پیام‌های data-toast را خودکار نمایش می‌دهد و از الگوی colored-toast تمپلیت yzen استفاده می‌کند.
        var script = await next.GetStringAsync("/assets/js/app-toast.js");
        Assert.Contains("[data-toast]", script);
        Assert.Contains("colored-toast", script);
        Assert.Contains("top-0 start-50 translate-middle-x", script); // وسط افقی بالای صفحه
        Assert.Contains("top-0 start-50 translate-middle-x", html);
        Assert.DoesNotContain("top-0 start-0 p-3", html);
        Assert.Contains("textContent", script); // متن پیام هرگز به‌صورت HTML درج نمی‌شود
        Assert.DoesNotContain("innerHTML", script);
    }
}
