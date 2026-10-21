extern alias PortalEntry;
extern alias AdminEntry;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrganizationIntranet.Api.Data;
using OrganizationIntranet.Application.Services;
using OrganizationIntranet.Contracts;
using OrganizationIntranet.Domain.Entities;
using Xunit;

namespace OrganizationIntranet.Tests;

// اخبار تصویری، صفحه‌های داخلی پورتال (اعلان‌ها، درخواست دسترسی، تنظیمات حساب، امنیت) و ظاهر هدر/منوی پنل مدیریت.
public sealed partial class PortalSmokeTests
{
    private static readonly byte[] TinyPng = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");

    private long SeedPublication(string title, bool published, bool isPublic)
    {
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var author = db.Users.Single(u => u.Username == "admin").UserId;
        var now = DateTime.UtcNow;
        var p = new Publication { Title = title, Summary = "خلاصه " + title, Body = "متن", Kind = "News", IsPublished = published, IsPublic = isPublic,
            PublishedAt = published ? now : null, CreatedAt = now, UpdatedAt = now, CreatedBy = author, UpdatedBy = author };
        db.Publications.Add(p);
        db.SaveChanges();
        return p.PublicationId;
    }

    private HttpClient AnonymousApi()
    {
        var anonymous = api.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        anonymous.DefaultRequestHeaders.Add("X-Intranet-Client", ApiFactory.ClientKey);
        return anonymous;
    }

    [Theory]
    [InlineData(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0, 0 }, "image/jpeg")]
    [InlineData(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0 }, "image/png")]
    [InlineData(new byte[] { 0x52, 0x49, 0x46, 0x46, 1, 2, 3, 4, 0x57, 0x45, 0x42, 0x50 }, "image/webp")]
    [InlineData(new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61 }, null)]     // GIF
    [InlineData(new byte[] { 0x3C, 0x73, 0x76, 0x67 }, null)]                 // <svg — اسکریپت‌پذیر، رد می‌شود
    [InlineData(new byte[0], null)]
    public void Image_type_is_detected_from_file_signature_only(byte[] data, string? expected) =>
        Assert.Equal(expected, PublicationService.DetectImageType(data));

    [Fact]
    public async Task Publication_images_follow_publication_visibility_and_reject_non_images()
    {
        var publicId = SeedPublication("عمومی", true, true);
        var internalId = SeedPublication("داخلی", true, false);
        var draftId = SeedPublication("پیش‌نویس", false, false);
        await Login();
        foreach (var id in new[] { publicId, internalId, draftId })
            (await client.PostAsJsonAsync($"/api/admin/publications/{id}/image", new PublicationImageUpload(Convert.ToBase64String(TinyPng)))).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/api/admin/publications/{publicId}/image", new PublicationImageUpload(Convert.ToBase64String("GIF89a"u8.ToArray())))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/api/admin/publications/{publicId}/image", new PublicationImageUpload("not-base64!"))).StatusCode);
        var huge = new byte[PublicationImageRules.MaxBytes + 1]; TinyPng.CopyTo(huge, 0);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/api/admin/publications/{publicId}/image", new PublicationImageUpload(Convert.ToBase64String(huge)))).StatusCode);

        var listed = (await client.GetFromJsonAsync<PublicationListDto>("/api/admin/publications"))!.Items.Single(p => p.PublicationId == publicId);
        Assert.True(listed.HasImage);
        Assert.NotEqual(0, listed.ImageVersion);

        var admin = await client.GetAsync($"/api/admin/publications/{draftId}/image");
        admin.EnsureSuccessStatusCode();
        Assert.Equal("image/png", admin.Content.Headers.ContentType!.MediaType);
        Assert.Equal("nosniff", admin.Headers.GetValues("X-Content-Type-Options").Single());

        using var visitor = AnonymousApi();
        Assert.Equal(HttpStatusCode.OK, (await visitor.GetAsync($"/api/public/publications/{publicId}/image")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await visitor.GetAsync($"/api/public/publications/{internalId}/image")).StatusCode); // داخلی برای ناشناس نیست
        Assert.Equal(HttpStatusCode.NotFound, (await visitor.GetAsync($"/api/public/publications/{draftId}/image")).StatusCode);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login("member"));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/publications/{internalId}/image")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/publications/{draftId}/image")).StatusCode); // پیش‌نویس فقط برای مدیر

        await Login();
        (await client.PostAsync($"/api/admin/publications/{publicId}/image/remove", null)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NotFound, (await visitor.GetAsync($"/api/public/publications/{publicId}/image")).StatusCode);
        Assert.False((await client.GetFromJsonAsync<PublicationDto>($"/api/admin/publications/{publicId}"))!.HasImage);
    }

    [Fact]
    public async Task Admin_uploads_a_news_image_and_the_portal_carousel_shows_it_above_my_systems()
    {
        var (admin, browser, _, _, _) = await AdminBrowserAsync();
        using (admin) using (browser)
        {
            using var form = new MultipartFormDataContent
            {
                { new StringContent("خبر تصویری"), "Input.Title" }, { new StringContent("خلاصه‌ی خبر"), "Input.Summary" }, { new StringContent("متن"), "Input.Body" },
                { new StringContent("News"), "Input.Kind" }, { new StringContent("true"), "Input.IsPublished" }, { new StringContent("true"), "Input.IsPublic" },
                { new StringContent(await Antiforgery(browser, "/Admin/Publications/Edit")), "__RequestVerificationToken" },
            };
            var file = new ByteArrayContent(TinyPng); file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            form.Add(file, "ImageFile", "news.png");
            Assert.Equal(HttpStatusCode.Redirect, (await browser.PostAsync("/Admin/Publications/Edit", form)).StatusCode);

            long id;
            using (var scope = api.Services.CreateScope())
                id = (await scope.ServiceProvider.GetRequiredService<AppDbContext>().Publications.SingleAsync(p => p.Title == "خبر تصویری")).PublicationId;
            var editPage = await browser.GetStringAsync($"/Admin/Publications/Edit?id={id}");
            Assert.Contains("enctype=\"multipart/form-data\"", editPage);
            Assert.Matches($"src=\"/Admin/Publications/Image\\?id={id}&amp;v=\\d+\"", editPage); // پیش‌نمایش تصویر فعلی
            (await browser.GetAsync($"/Admin/Publications/Image?id={id}")).EnsureSuccessStatusCode();
            Assert.Contains("item-card__thumb", await browser.GetStringAsync("/Admin/Publications"));
        }

        using var portal = Ui<PortalEntry::Program>("Portal");
        using var visitor = portal.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var html = await visitor.GetStringAsync("/Portal");
        Assert.True(html.IndexOf("id=\"news\"", StringComparison.Ordinal) < html.IndexOf("id=\"systems\"", StringComparison.Ordinal)); // اخبار بالای سامانه‌ها
        Assert.Contains("data-news-carousel", html);
        Assert.Contains("news-carousel.js", html);
        var src = Regex.Match(html, "src=\"(/Publications/Image/\\d+\\?v=\\d+)\"").Groups[1].Value;
        Assert.NotEmpty(src);
        var image = await visitor.GetAsync(src);
        image.EnsureSuccessStatusCode();
        Assert.Equal("image/png", image.Content.Headers.ContentType!.MediaType);
        Assert.DoesNotContain("این بخش برای همه پرسنل یکسان است", WebUtility.HtmlDecode(html)); // متن توضیحی اضافه حذف شد
        Assert.DoesNotContain("section-desc", html);
        Assert.Equal(HttpStatusCode.NotFound, (await visitor.GetAsync("/Publications/Image/999999")).StatusCode);
    }

    [Fact]
    public async Task Signed_in_portal_pages_share_the_portal_shell_and_account_menu()
    {
        var token = await Login("member");
        using var portal = Ui<PortalEntry::Program>("Portal");
        using var browser = portal.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        browser.DefaultRequestHeaders.Add("Cookie", Cookie(portal.Services, token, false));
        foreach (var (path, title) in new[] { ("/Account/Profile", "پروفایل من"), ("/Account/Settings", "تنظیمات حساب"), ("/Account/Security", "امنیت حساب و Google Authenticator"),
                     ("/Notifications", "اعلان‌ها"), ("/AccessRequests", "درخواست دسترسی به سامانه‌ها") })
        {
            var response = await browser.GetAsync(path);
            Assert.True(response.StatusCode == HttpStatusCode.OK, $"{path} → {(int)response.StatusCode}");
            var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
            Assert.Contains("portal-shell.css", html);
            Assert.Contains($"<h1>{title}</h1>", html);
            foreach (var link in new[] { "/Notifications", "/AccessRequests", "/Account/Settings", "/Account/Profile", "/Account/Logout" }) Assert.Contains($"href=\"{link}\"", html);
            Assert.DoesNotContain("auth-shell", html); // صفحه‌ی امنیت دیگر پوسته‌ی ساده‌ی ورود را ندارد
        }

        // منوی حساب در صفحه‌ی اصلی به همان صفحه‌ها می‌رود؛ مدال کارت سامانه‌ها جمع‌وجور و بدون «درخواست‌های من» است.
        var home = WebUtility.HtmlDecode(await browser.GetStringAsync("/Portal"));
        Assert.Contains("href=\"/AccessRequests\"", home);
        Assert.Contains("href=\"/Account/Settings\"", home);
        Assert.Contains("href=\"/Notifications\"", home);
        var dialog = Regex.Match(home, "<dialog id=\"access-requests\"[\\s\\S]*?</dialog>").Value;
        Assert.Matches("<button type=\"button\" class=\"access-requests-close\" aria-label=\"بستن\"[^>]*>\\s*<svg", dialog); // فقط آیکن بستن
        Assert.DoesNotContain("بستن ×", dialog);
        Assert.DoesNotContain("درخواست‌های من</h3>", dialog);
        Assert.Contains(".access-requests-dialog{width:min(560px", await browser.GetStringAsync("/css/portal-access-requests.css"));
    }

    [Fact]
    public async Task Notifications_page_filters_by_read_state_and_marks_all_read()
    {
        using (var scope = api.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var member = await db.Users.SingleAsync(u => u.Username == "member");
            var app = await db.Applications.SingleAsync(); app.IsPublic = true;
            app.Services.Add(new() { ServiceCode = "NOTIFICATIONS", DisplayName = "اعلان" });
            db.UserNotifications.AddRange(
                new UserNotification { UserId = member.UserId, Notification = new() { Application = app, Title = "اعلان تازه", Message = "متن" } },
                new UserNotification { UserId = member.UserId, IsRead = true, ReadAt = DateTime.UtcNow, Notification = new() { Application = app, Title = "اعلان خوانده‌شده", Message = "متن" } });
            await db.SaveChangesAsync();
        }
        var token = await Login("member");
        using var portal = Ui<PortalEntry::Program>("Portal");
        using var browser = portal.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        browser.DefaultRequestHeaders.Add("Cookie", Cookie(portal.Services, token, false));

        var all = WebUtility.HtmlDecode(await browser.GetStringAsync("/Notifications"));
        Assert.Contains("اعلان تازه", all); Assert.Contains("اعلان خوانده‌شده", all);
        Assert.Contains("class=\"pp-count\"", all); // شمار خوانده‌نشده روی زنگ هدر
        var unread = WebUtility.HtmlDecode(await browser.GetStringAsync("/Notifications?tab=unread"));
        Assert.Contains("اعلان تازه", unread); Assert.DoesNotContain("اعلان خوانده‌شده", unread);
        var read = WebUtility.HtmlDecode(await browser.GetStringAsync("/Notifications?tab=read"));
        Assert.DoesNotContain("اعلان تازه", read); Assert.Contains("اعلان خوانده‌شده", read);

        var csrf = await Antiforgery(browser, "/Notifications");
        Assert.Equal(HttpStatusCode.Redirect, (await browser.PostAsync("/Notifications?handler=ReadAll", new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = csrf }))).StatusCode);
        Assert.DoesNotContain("اعلان تازه", WebUtility.HtmlDecode(await browser.GetStringAsync("/Notifications?tab=unread")));
    }

    [Fact]
    public async Task Admin_header_bell_menu_toggle_and_tables_follow_the_yzen_template()
    {
        var (admin, browser, _, _, _) = await AdminBrowserAsync();
        using (admin) using (browser)
        {
            foreach (var path in new[] { "/Admin", "/Account/Profile", "/Account/Security" })
            {
                var html = await browser.GetStringAsync(path);
                Assert.Contains("notifications-dropdown", html);           // زنگ در پروفایل هم نمایش داده می‌شود
                Assert.Contains("class=\"header-link-icon\"", html);       // آیکن SVG هم‌شکل آیکن تم
                Assert.DoesNotContain("ti ti-bell fs-20", html);
                Assert.Contains("animated-arrow hor-toggle", html);          // دکمه‌ی جمع‌کردن منو در هدر دیده می‌شود
            }
            var dashboard = WebUtility.HtmlDecode(await browser.GetStringAsync("/Admin"));
            var order = new[] { "سامانه‌ها و محتوا", "کاربران و دسترسی", "تنظیمات" }.Select(c => dashboard.IndexOf($">{c}<", StringComparison.Ordinal)).ToArray();
            Assert.All(order, i => Assert.True(i > 0));
            Assert.True(order[0] < order[1] && order[1] < order[2]); // «کاربران و دسترسی» زیر «سامانه‌ها و محتوا» و بالای «تنظیمات»

            var users = WebUtility.HtmlDecode(await browser.GetStringAsync("/Admin/Users"));
            Assert.Contains("<th class=\"col-actions\">عملیات</th>", users); // نام ستون عملیات نمایش داده می‌شود
            Assert.DoesNotContain("visually-hidden\">عملیات", users);

            var security = WebUtility.HtmlDecode(await browser.GetStringAsync("/Account/Security"));
            Assert.Contains("class=\"admin-page-head\"", security); // صفحه‌ی امنیت در پوسته‌ی پنل مدیریت
            Assert.Contains("security-steps", security);
        }
    }
}
