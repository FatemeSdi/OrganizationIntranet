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
using OrganizationIntranet.Contracts;
using Xunit;

namespace OrganizationIntranet.Tests;

// تصویر کارت سامانه (migration 020): آپلود در پنل، دامنه‌ی دید تصویر در API و نمایش روی کارت پورتال به‌جای آیکن.
public sealed partial class PortalSmokeTests
{
    private static readonly byte[] CardPng = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");

    /// <summary>سامانه‌ی ساده‌ی فعال با نشانی ایمن، مستقیم در بانک.</summary>
    private int SeedCardApplication(string code, bool isPublic)
    {
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var app = new OrganizationIntranet.Domain.Entities.Application { ApplicationName = "سامانه " + code, ApplicationCode = code, BaseUrl = "https://" + code.ToLower() + ".example/", IsActive = true, IsPublic = isPublic };
        db.Applications.Add(app); db.SaveChanges();
        return app.ApplicationId;
    }

    private Task<HttpResponseMessage> UploadCardImage(int id, byte[] bytes) => client.PostAsJsonAsync($"/api/admin/applications/{id}/image", new ApplicationImageUpload(Convert.ToBase64String(bytes)));

    [Fact]
    public async Task Application_image_is_validated_by_file_signature_and_size_and_can_be_removed()
    {
        var id = SeedCardApplication("IMGVAL", true);
        await Login();
        Assert.False((await client.GetFromJsonAsync<ApplicationConfigurationDto>($"/api/admin/applications/{id}"))!.Application.HasImage);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/admin/applications/{id}/image")).StatusCode);   // هنوز تصویری نیست

        Assert.Equal(HttpStatusCode.BadRequest, (await UploadCardImage(id, "GIF89a"u8.ToArray())).StatusCode);              // نوع از امضا، نه پسوند
        Assert.Equal(HttpStatusCode.BadRequest, (await UploadCardImage(id, "<svg onload=alert(1)></svg>"u8.ToArray())).StatusCode); // SVG اسکریپت‌پذیر است
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/api/admin/applications/{id}/image", new ApplicationImageUpload("not-base64!"))).StatusCode);
        var huge = new byte[ApplicationImageRules.MaxBytes + 1]; CardPng.CopyTo(huge, 0);
        Assert.Equal(HttpStatusCode.BadRequest, (await UploadCardImage(id, huge)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await UploadCardImage(999999, CardPng)).StatusCode);                          // سامانه‌ی ناموجود

        (await UploadCardImage(id, CardPng)).EnsureSuccessStatusCode();
        var dto = (await client.GetFromJsonAsync<ApplicationConfigurationDto>($"/api/admin/applications/{id}"))!.Application;
        Assert.True(dto.HasImage);
        var list = (await client.GetFromJsonAsync<List<ApplicationDto>>("/api/admin/applications"))!;
        Assert.Equal(dto.ImageVersion, list.Single(a => a.ApplicationId == id).ImageVersion);
        var image = await client.GetAsync($"/api/admin/applications/{id}/image");
        image.EnsureSuccessStatusCode();
        Assert.Equal("image/png", image.Content.Headers.ContentType!.MediaType);
        Assert.Equal("nosniff", image.Headers.GetValues("X-Content-Type-Options").Single());

        (await UploadCardImage(id, CardPng)).EnsureSuccessStatusCode();                                                      // جایگزینی، نه ردیف دوم
        using (var scope = api.Services.CreateScope())
            Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<AppDbContext>().ApplicationImages.CountAsync(i => i.ApplicationId == id));

        (await client.PostAsync($"/api/admin/applications/{id}/image/remove", null)).EnsureSuccessStatusCode();
        (await client.PostAsync($"/api/admin/applications/{id}/image/remove", null)).EnsureSuccessStatusCode();             // تکرار بی‌اثر است
        Assert.False((await client.GetFromJsonAsync<ApplicationConfigurationDto>($"/api/admin/applications/{id}"))!.Application.HasImage);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/admin/applications/{id}/image")).StatusCode);
    }

    [Fact]
    public async Task Application_image_is_served_only_to_those_who_can_see_the_application()
    {
        var publicId = SeedCardApplication("IMGPUB", true);
        var privateId = SeedCardApplication("IMGPRIV", false);
        await Login();
        foreach (var id in new[] { publicId, privateId }) (await UploadCardImage(id, CardPng)).EnsureSuccessStatusCode();

        using var visitor = AnonymousApi();
        var publicApps = (await visitor.GetFromJsonAsync<List<PublicApplicationDto>>("/api/public/applications"))!;
        Assert.NotEqual(0, publicApps.Single(a => a.ApplicationId == publicId).ImageVersion);
        var served = await visitor.GetAsync($"/api/public/applications/{publicId}/image");
        served.EnsureSuccessStatusCode();
        Assert.Equal("image/png", served.Content.Headers.ContentType!.MediaType);
        Assert.Equal("nosniff", served.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal(HttpStatusCode.NotFound, (await visitor.GetAsync($"/api/public/applications/{privateId}/image")).StatusCode); // غیرعمومی برای ناشناس لو نمی‌رود

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login("member"));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/portal/applications/{publicId}/image")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/portal/applications/{privateId}/image")).StatusCode); // بدون دسترسی
        var card = (await client.GetFromJsonAsync<List<PortalApplicationDto>>("/api/portal/applications"))!.Single(a => a.ApplicationId == publicId);
        Assert.True(card.HasImage);

        await Login();
        var memberId = MemberId();
        (await client.PostAsJsonAsync($"/api/admin/applications/{privateId}/access", new AccessToggleRequest(memberId, null, true))).EnsureSuccessStatusCode();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login("member"));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/portal/applications/{privateId}/image")).StatusCode);       // با تخصیص دسترسی

        await Login();
        (await client.PostAsync($"/api/admin/applications/{publicId}/image/remove", null)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NotFound, (await visitor.GetAsync($"/api/public/applications/{publicId}/image")).StatusCode);
        // سامانه‌ی غیرفعال حتی با تصویر برای ناشناس دیده نمی‌شود
        await client.PostAsync($"/api/admin/applications/{privateId}/toggle", null);
        (await UploadCardImage(publicId, CardPng)).EnsureSuccessStatusCode();
        await client.PostAsync($"/api/admin/applications/{publicId}/toggle", null);
        Assert.Equal(HttpStatusCode.NotFound, (await visitor.GetAsync($"/api/public/applications/{publicId}/image")).StatusCode);
    }

    [Fact]
    public async Task Admin_form_uploads_and_removes_the_card_image_and_the_portal_card_shows_it_instead_of_the_icon()
    {
        var withImage = SeedCardApplication("CARDIMG", true);
        var withoutImage = SeedCardApplication("CARDICON", true);
        var (admin, browser, _, _, _) = await AdminBrowserAsync();
        using (admin) using (browser)
        {
            var path = $"/Admin/Applications/Edit?id={withImage}";
            var page = await browser.GetStringAsync(path);
            Assert.Contains("enctype=\"multipart/form-data\"", page);
            Assert.Contains("name=\"ImageFile\"", page);                        // انتخاب تصویر کنار انتخاب آیکن
            Assert.True(page.IndexOf("data-icon-picker", StringComparison.Ordinal) < page.IndexOf("name=\"ImageFile\"", StringComparison.Ordinal));
            Assert.DoesNotContain("name=\"RemoveImage\"", page);                // تا تصویری نیست گزینه‌ی حذف نیست

            MultipartFormDataContent Form(bool withFile, bool remove, string token)
            {
                var form = new MultipartFormDataContent
                {
                    { new StringContent(withImage.ToString()), "Id" }, { new StringContent("سامانه CARDIMG"), "Input.ApplicationName" }, { new StringContent("CARDIMG"), "Input.ApplicationCode" },
                    { new StringContent("https://cardimg.example/"), "Input.BaseUrl" }, { new StringContent("apps"), "Input.Icon" }, { new StringContent("InApp"), "Input.AccessRequestNotificationChannels" },
                    { new StringContent("Public"), "Input.AccessMode" }, { new StringContent("true"), "Input.RequiresLogin" }, { new StringContent(token), "__RequestVerificationToken" },
                };
                if (remove) form.Add(new StringContent("true"), "RemoveImage");
                if (withFile) { var file = new ByteArrayContent(CardPng); file.Headers.ContentType = new MediaTypeHeaderValue("image/png"); form.Add(file, "ImageFile", "card.png"); }
                return form;
            }

            using (var upload = Form(true, false, await Antiforgery(browser, path)))
                Assert.Equal(HttpStatusCode.Redirect, (await browser.PostAsync(path, upload)).StatusCode);
            var edited = await browser.GetStringAsync(path);
            Assert.Matches($"src=\"/Admin/Applications/Image\\?id={withImage}&amp;v=\\d+\"", edited);       // پیش‌نمایش تصویر فعلی
            Assert.Contains("name=\"RemoveImage\"", edited);
            var preview = await browser.GetAsync($"/Admin/Applications/Image?id={withImage}");
            preview.EnsureSuccessStatusCode();
            Assert.Equal("image/png", preview.Content.Headers.ContentType!.MediaType);
            Assert.Equal(HttpStatusCode.NotFound, (await browser.GetAsync($"/Admin/Applications/Image?id={withoutImage}")).StatusCode);

            // فایل غیرتصویری: تنظیمات ذخیره می‌شود ولی تصویر رد می‌شود و پیام جدا می‌آید
            using (var bad = Form(false, false, await Antiforgery(browser, path)))
            {
                var file = new ByteArrayContent("GIF89a-not-allowed"u8.ToArray()); file.Headers.ContentType = new MediaTypeHeaderValue("image/png"); bad.Add(file, "ImageFile", "x.png");
                Assert.Equal(HttpStatusCode.Redirect, (await browser.PostAsync(path, bad)).StatusCode);
            }
            Assert.Contains("تصویر پذیرفته نشد", WebUtility.HtmlDecode(await browser.GetStringAsync(path)));

            // نمایش روی کارت پورتال (ناشناس): کارت دارای تصویر، تصویر؛ کارت بدون تصویر همان آیکن
            using var portal = Ui<PortalEntry::Program>("Portal");
            using var visitor = portal.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
            var html = await visitor.GetStringAsync("/Portal");
            var src = Regex.Match(html, $"src=\"(/Applications/Image/{withImage}\\?v=\\d+)\"").Groups[1].Value;
            Assert.NotEmpty(src);
            Assert.Contains("class=\"sys-media\"", html);
            // کارت دارای تصویر: نام و توضیح در نوار پایین کارت به‌جای «ورود به سامانه»؛ کارت بدون تصویر مثل قبل
            var decoded = WebUtility.HtmlDecode(html);
            var imageCard = Regex.Match(decoded, "<a class=\"sys-card sys-card--image\".*?</a>", RegexOptions.Singleline).Value;
            Assert.Contains("class=\"go sys-caption\"", imageCard);
            Assert.Contains("<h3>سامانه CARDIMG</h3>", imageCard);
            Assert.DoesNotContain("ورود به سامانه", imageCard);
            var plainCard = Regex.Match(decoded, "<a class=\"sys-card\" [^>]*data-name=\"سامانه CARDICON.*?</a>", RegexOptions.Singleline).Value;
            Assert.Contains("ورود به سامانه", plainCard);
            Assert.DoesNotContain("sys-media", plainCard);
            Assert.DoesNotContain($"/Applications/Image/{withoutImage}", html);
            var image = await visitor.GetAsync(src);
            image.EnsureSuccessStatusCode();
            Assert.Equal("image/png", image.Content.Headers.ContentType!.MediaType);
            Assert.Equal("nosniff", image.Headers.GetValues("X-Content-Type-Options").Single());
            Assert.Equal(HttpStatusCode.NotFound, (await visitor.GetAsync($"/Applications/Image/{withoutImage}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await visitor.GetAsync("/Applications/Image/999999")).StatusCode);

            // حذف از فرم: کارت دوباره آیکن را نشان می‌دهد
            using (var remove = Form(false, true, await Antiforgery(browser, path)))
                Assert.Equal(HttpStatusCode.Redirect, (await browser.PostAsync(path, remove)).StatusCode);
            Assert.DoesNotContain($"/Applications/Image/{withImage}", await visitor.GetStringAsync("/Portal"));
            Assert.DoesNotContain("name=\"RemoveImage\"", await browser.GetStringAsync(path));
        }
    }
}
