extern alias AdminEntry;
using System.Net;
using System.Text.RegularExpressions;
using Xunit;

namespace OrganizationIntranet.Tests;

// ستاره‌ی قرمز کنار عنوان همه‌ی فیلدهای اجباری پنل: یک اسکریپت مشترک (admin-ui.js) و فیلدهایی که سرور اجباری می‌داند required دارند.
public sealed partial class PortalSmokeTests
{
    [Fact]
    public async Task Admin_script_stars_every_required_field_label_once()
    {
        var (admin, browser, _, _, _) = await AdminBrowserAsync();
        using (admin) using (browser)
        {
            var js = await browser.GetStringAsync("/assets/js/admin-ui.js");
            Assert.Contains("markRequiredFields", js);
            Assert.Contains("input[required]", js);
            Assert.DoesNotContain("input[data-val-required]", js);              // سوییچ bool نباید به‌خاطر data-val-required اجباری شود
            Assert.Contains("type === 'checkbox'", js);
            Assert.Contains("required-star", js);
            Assert.Contains("hasStar", js);                                    // ستاره‌ی دستی موجود دوباره اضافه نمی‌شود
            Assert.Contains("headerOf", js);                                   // جدول‌های بی‌label: سرستون ستون
            Assert.Contains("data-no-required-star", js);                      // راه استثنا
            Assert.True(js.LastIndexOf("markRequiredFields", StringComparison.Ordinal) > js.IndexOf("table.table-admin", StringComparison.Ordinal)); // بعد از برچسب‌گذاری کارتی جدول
            Assert.Contains(".required-star", await browser.GetStringAsync("/assets/css/admin-ui.css"));
        }
    }

    [Fact]
    public async Task Admin_forms_mark_server_required_fields_with_the_required_attribute()
    {
        var (admin, browser, _, _, _) = await AdminBrowserAsync();
        using (admin) using (browser)
        {
            var create = WebUtility.HtmlDecode(await browser.GetStringAsync("/Admin/Users/Edit"));
            foreach (var name in new[] { "NewPassword", "Name", "LastName", "Username" })
                Assert.Contains(" required", Regex.Match(create, $"<input[^>]*name=\"{name}\"[^>]*>").Value, StringComparison.Ordinal);   // کاربر جدید رمز هم لازم دارد

            foreach (var page in new[] { "/Admin/Roles", "/Admin/Permissions", "/Admin/Applications" })
            {
                var html = WebUtility.HtmlDecode(await browser.GetStringAsync(page));
                Assert.Contains("/assets/js/admin-ui.js", html);                                               // اسکریپت ستاره در همه‌ی صفحه‌ها
                Assert.Matches(new Regex("<(input|select)[^>]* required"), html);
            }
        }
    }
}
