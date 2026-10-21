extern alias AdminEntry;
extern alias PortalEntry;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using OrganizationIntranet.Api.Data;
using OrganizationIntranet.Contracts;
using OrganizationIntranet.Domain.Entities;
using Xunit;

namespace OrganizationIntranet.Tests;

// بازطراحی پنل مدیریت روی yzen: همه‌ی صفحات با سربرگ یکسان رندر می‌شوند و فیلدهای فرم همان نام‌های قبلی را ارسال می‌کنند.
public sealed partial class PortalSmokeTests
{
    private async Task<(WebApplicationFactory<AdminEntry::Program> Admin, HttpClient Browser, string Token, long MemberId, int PermissionId)> AdminBrowserAsync()
    {
        long memberId; int permissionId;
        using (var scope = api.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            memberId = db.Users.Single(u => u.Username == "member").UserId;
            var permission = db.Permissions.FirstOrDefault();
            if (permission is null)
            {
                permission = new Permission { ApplicationId = db.Applications.First().ApplicationId, PermissionName = "مشاهده", PermissionCode = "REDESIGN_VIEW" };
                db.Permissions.Add(permission);
                db.SaveChanges();
            }
            permissionId = permission.PermissionId;
        }
        var token = await Login();
        var admin = Ui<AdminEntry::Program>("Admin");
        var browser = admin.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        browser.DefaultRequestHeaders.Add("Cookie", Cookie(admin.Services, token, true));
        return (admin, browser, token, memberId, permissionId);
    }

    [Fact]
    public async Task Every_admin_page_renders_with_the_shared_page_header_and_admin_ui_assets()
    {
        var (admin, browser, _, memberId, permissionId) = await AdminBrowserAsync();
        using (admin) using (browser)
        {
            var pages = new Dictionary<string, string>
            {
                ["/Admin/Users"] = "کاربران", ["/Admin/Users/Edit"] = "افزودن کاربر", [$"/Admin/Users/Edit?id={memberId}"] = "ویرایش کاربر",
                ["/Admin/Roles"] = "نقش‌ها", ["/Admin/Permissions"] = "مجوزها (Permission)", [$"/Admin/Permissions/Assign?id={permissionId}"] = "اختصاص مجوز به نقش‌ها",
                ["/Admin/Applications"] = "سامانه‌ها", ["/Admin/Applications/Edit?id=1"] = "تنظیمات سامانه", ["/Admin/Applications/Supervision?id=1"] = "گروه‌های نظارت",
                ["/Admin/Applications/Import"] = "ثبت فهرست سامانه‌های سازمان", ["/Admin/AccessRequests"] = "درخواست‌های دسترسی",
                ["/Admin/AccessRequests/Notifications"] = "پیامک درخواست‌های دسترسی", ["/Admin/Publications"] = "اطلاع‌رسانی سازمانی",
                ["/Admin/Publications/Edit"] = "افزودن محتوا", ["/Admin/Authentication"] = "ورود و امنیت",
            };
            foreach (var (path, title) in pages)
            {
                var response = await browser.GetAsync(path);
                Assert.True(response.StatusCode == HttpStatusCode.OK, $"{path} → {(int)response.StatusCode}");
                var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
                Assert.Single(Regex.Matches(html, "class=\"admin-page-head\""));          // یک سربرگ، بدون مسیر تکراری قدیمی
                Assert.Single(Regex.Matches(html, "<h1[ >]"));                            // یک عنوان اصلی در هر صفحه
                Assert.Matches($"<h1><i class=\"[^\"]+\" aria-hidden=\"true\"></i>{Regex.Escape(title)}</h1>", html);
                var head = Regex.Match(html, "<header class=\"admin-page-head\">[\\s\\S]*?</header>").Value;
                Assert.Single(Regex.Matches(head, Regex.Escape(title) + "(?=<)")); // عنوان در سربرگ فقط یک‌بار؛ در مسیر صفحه تکرار نمی‌شود
                Assert.DoesNotContain("breadcrumb-item active", html);
                Assert.Equal(path.Contains("/Edit") || path.Contains("/Assign") || path.Contains("/Supervision") || path.Contains("/Import") || path.Contains("/Notifications"),
                    html.Contains("admin-page-head__parent"));                           // مسیر فقط در صفحه‌های فرعی و فقط تا والد
                Assert.Matches("admin-ui\\.css\\?v=", html);
                Assert.Matches("admin-ui\\.js\\?v=", html);
            }

            var profile = WebUtility.HtmlDecode(await browser.GetStringAsync("/Account/Profile"));
            Assert.DoesNotContain("class=\"admin-page-head\"", profile); // پروفایل سربرگ اختصاصی خودش را دارد

            // داشبورد: بدون سربرگ صفحه؛ فقط کادر خوش‌آمد روشن با نمای بندر، و «مشاهده همه» به شکل دکمه
            var dashboard = WebUtility.HtmlDecode(await browser.GetStringAsync("/Admin"));
            Assert.DoesNotContain("class=\"admin-page-head\"", dashboard);
            Assert.Contains("class=\"admin-hero", dashboard);
            Assert.Contains("class=\"port-art admin-hero__art\"", dashboard); // نمای بندر درون‌خطی تا تم تیره ماه را نشان دهد
            Assert.Contains("class=\"pi-sun\"", dashboard);
            Assert.Contains("class=\"pi-moon\"", dashboard);
            Assert.Matches("<a href=\"/Admin/Users\" class=\"btn btn-sm btn-primary-light\">مشاهده همه", dashboard);

            foreach (var path in new[] { "/Admin/Users", "/Admin/Roles", "/Admin/Permissions", "/Admin/Applications" })
            {
                var list = await browser.GetStringAsync(path);
                Assert.Contains("data-filter-scope data-page-size=\"10\"", list); // جدول‌ها صفحه‌بندی می‌شوند
                Assert.Contains("data-pager", list);
                Assert.DoesNotContain("w-100\"><i class=\"ti ti-plus", list);    // دکمه‌ی ثبت کادر کناری تمام‌عرض نیست
                Assert.DoesNotContain("<a href=\"/Admin/Permissions\" class=\"btn btn-light border\"", list); // لینک‌های هدایت رنگی‌اند
            }
            var users = await browser.GetStringAsync("/Admin/Users");
            Assert.Contains("data-filter=\"quick\"", users);
            Assert.Contains("id=\"usersAdvancedSearch\"", users); // جستجوی پیشرفته با فیلدهای جدا
            foreach (var key in new[] { "name", "username", "nationalid", "mobile", "roles", "status", "login" }) Assert.Contains($"data-filter=\"{key}\"", users);
            Assert.Contains("data-filter-chips", users);
            Assert.Contains("data-filter-empty", users);

            var script = await browser.GetStringAsync("/assets/js/admin-ui.js");
            Assert.Contains("data-confirm-off", script);
            Assert.Contains("pagination", script);
            var css = await browser.GetStringAsync("/assets/css/admin-ui.css");
            Assert.Contains(".form-actions { justify-content: flex-end; }", css); // دکمه‌ی ثبت پایین سمت چپ
            Assert.DoesNotContain("innerHTML", script); // متن کاربر هرگز به‌صورت HTML درج نمی‌شود
            (await browser.GetAsync("/assets/css/admin-ui.css")).EnsureSuccessStatusCode();
        }
    }

    [Fact]
    public void Server_pager_shows_first_last_and_neighbour_pages_with_gaps()
    {
        var pager = new AdminEntry::OrganizationIntranet.Pages.Shared.ServerPager(5, 20, 200, "مطلب", new Dictionary<string, string?> { ["status"] = "Pending" });
        Assert.Equal(10, pager.TotalPages);
        Assert.Equal([1, 0, 3, 4, 5, 6, 7, 0, 10], pager.Numbers());
        Assert.Equal([1], new AdminEntry::OrganizationIntranet.Pages.Shared.ServerPager(1, 20, 0, "مطلب").Numbers());
        var route = pager.RouteFor(6);
        Assert.Equal("6", route["pageNumber"]);
        Assert.Equal("Pending", route["status"]); // فیلترها در پیوند صفحه حفظ می‌شوند
    }

    [Fact]
    public async Task Server_paged_lists_render_numbered_pagination_that_keeps_filters()
    {
        var (admin, browser, _, memberId, _) = await AdminBrowserAsync();
        using (admin) using (browser)
        {
            using (var scope = api.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                for (var i = 0; i < 25; i++)
                    db.Publications.Add(new Publication { Title = $"مطلب {i}", Summary = "S", Body = "B", Kind = "News", CreatedBy = memberId, UpdatedBy = memberId, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
                db.SaveChanges();
            }
            var response = await browser.GetAsync("/Admin/Publications?kind=News");
            var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
            Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {html[..Math.Min(html.Length, 3000)]}");
            Assert.Contains("class=\"pagination pagination-sm\"", html);
            Assert.Matches("href=\"/Admin/Publications\\?kind=News&pageNumber=2\"", html);
            Assert.Matches("aria-current=\"page\"[^>]*>1</a>", html);
        }
    }

    [Fact]
    public async Task Portal_profile_uses_the_portal_theme_and_login_method_is_an_obvious_dropdown()
    {
        var token = await Login();
        using var portal = Ui<PortalEntry::Program>("Portal");
        using var browser = portal.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var login = WebUtility.HtmlDecode(await browser.GetStringAsync("/Account/Login"));
        Assert.Contains("class=\"pl-select", login);                       // فهرست کشویی با آیکن و فلش واضح
        Assert.DoesNotContain("background:#FBFAF6!important", login);      // shorthand که فلش را پاک می‌کرد حذف شده
        Assert.Contains("background-position:left 14px center", login);

        browser.DefaultRequestHeaders.Add("Cookie", Cookie(portal.Services, token, false));
        var profile = WebUtility.HtmlDecode(await browser.GetStringAsync("/Account/Profile"));
        Assert.Contains("portal-shell.css", profile);                       // پوسته‌ی مشترک صفحه‌های داخلی پورتال
        Assert.Contains("--navy-950: #0A2036", await browser.GetStringAsync("/css/portal-shell.css")); // همان توکن‌های رنگ صفحه‌ی اصلی پورتال
        Assert.Contains("pp-identity", profile);
        Assert.Contains("logo-pmo.png", profile);
        Assert.Contains("href=\"/Account/Settings\"", profile);            // تغییر رمز به تنظیمات حساب منتقل شد
        Assert.DoesNotContain("profile-background", profile);              // طرح قدیمی yzen حذف شده
    }

    [Fact]
    public async Task Admin_login_has_a_separate_show_password_button_and_a_full_width_captcha_box()
    {
        using var admin = Ui<AdminEntry::Program>("Admin");
        using var browser = admin.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var html = WebUtility.HtmlDecode(await browser.GetStringAsync("/Account/Login"));

        // آیکن کلید در راست، فیلد رمز، دکمه‌ی چشم در چپ (مثل ورود سامانه‌ی مدیریت وظایف)
        Assert.Matches(new Regex("password-key-icon[^>]*>\\s*<i class=\"ti ti-key[\\s\\S]*?name=\"Password\"[\\s\\S]*?<button class=\"input-group-text password-toggle\" type=\"button\""), html);
        Assert.Contains("aria-label=\"نمایش رمز عبور\"", html);
        // کد امنیتی: کادر تمام‌عرض بدون برچسب، با متن راهنمای داخل کادر
        Assert.Contains("placeholder=\"کد امنیتی را وارد کنید\"", html);
        Assert.DoesNotContain("<label class='form-group'>کد امنیتی", html);
        Assert.DoesNotContain("<label class=\"form-group\">کد امنیتی", html);

        var script = await browser.GetStringAsync("/js/login-form.js");
        Assert.Contains("ti-eye-off", script);
        Assert.Contains("aria-pressed", script);
    }

    [Fact]
    public async Task Application_access_pick_list_posts_the_same_field_names_as_the_old_multi_select()
    {
        var (admin, browser, token, memberId, _) = await AdminBrowserAsync();
        using (admin) using (browser)
        {
            var html = await browser.GetStringAsync("/Admin/Applications/Edit?id=1");
            Assert.DoesNotContain("multiple", html); // select چندگانه با فهرست چک‌باکسی جایگزین شده
            Assert.Contains($"name=\"Input.UserIds\" value=\"{memberId}\"", html);
            Assert.Contains("name=\"Input.RoleIds\"", html);
            var supervision = await browser.GetStringAsync("/Admin/Applications/Supervision?id=1");
            Assert.Contains($"name=\"UserIds\" value=\"{memberId}\"", supervision);

            var fields = new List<KeyValuePair<string, string>>
            {
                new("Id", "1"), new("Input.ApplicationName", "سامانه آزمایشی"), new("Input.ApplicationCode", "PICK_TEST"), new("Input.Icon", "apps"),
                new("Input.AccessRequestNotificationChannels", "InApp"), new("Input.RequiresLogin", "true"), new("Input.UserIds", memberId.ToString()),
                new("__RequestVerificationToken", await Antiforgery(browser, "/Admin/Applications/Edit?id=1")),
            };
            Assert.Equal(HttpStatusCode.Redirect, (await browser.PostAsync("/Admin/Applications/Edit?id=1", new FormUrlEncodedContent(fields))).StatusCode);

            using var client = api.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
            client.DefaultRequestHeaders.Add("X-Intranet-Client", ApiFactory.ClientKey);
            client.DefaultRequestHeaders.Add("X-Intranet-Admin", ApiFactory.AdminClientKey);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var saved = (await client.GetFromJsonAsync<ApplicationConfigurationDto>("/api/admin/applications/1"))!;
            Assert.Equal([memberId], saved.UserIds);
            Assert.Equal("PICK_TEST", saved.Application.ApplicationCode);
        }
    }
}
