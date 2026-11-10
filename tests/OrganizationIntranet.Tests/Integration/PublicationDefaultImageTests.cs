extern alias AdminEntry;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrganizationIntranet.Api.Data;
using OrganizationIntranet.Contracts;
using Xunit;
using Defaults = AdminEntry::OrganizationIntranet.Pages.Admin.Publications.PublicationDefaultImages;

namespace OrganizationIntranet.Tests;

// تصویر پیش‌فرض کارت خبر: کنار انتخاب تصویر در مدیریت، و اگر مطلب تصویر نداشته باشد یکی خودکار انتخاب می‌شود.
public sealed partial class PortalSmokeTests
{
    private static string DefaultImagesDirectory() =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "wwwroot", "assets", "images", "publication-defaults"));
    private static byte[][] DefaultImageBytes() => Defaults.Names.Select(n => File.ReadAllBytes(Path.Combine(DefaultImagesDirectory(), n + ".png"))).ToArray();

    private async Task<long> SavePublicationPage(HttpClient browser, string title, Action<MultipartFormDataContent>? extra = null, long? id = null)
    {
        using var form = new MultipartFormDataContent
        {
            { new StringContent(title), "Input.Title" }, { new StringContent("خلاصه"), "Input.Summary" }, { new StringContent("متن"), "Input.Body" },
            { new StringContent("News"), "Input.Kind" }, { new StringContent("true"), "Input.IsPublished" }, { new StringContent("true"), "Input.IsPublic" },
            { new StringContent(await Antiforgery(browser, "/Admin/Publications/Edit")), "__RequestVerificationToken" },
        };
        if (id is not null)
        {
            long revision;
            using (var revisionScope = api.Services.CreateScope()) revision = (await revisionScope.ServiceProvider.GetRequiredService<AppDbContext>().Publications.AsNoTracking().SingleAsync(p => p.PublicationId == id)).Revision;
            form.Add(new StringContent(id.ToString()!), "Id"); form.Add(new StringContent(revision.ToString()), "Input.Revision");   // نسخه‌ی فعلی (آپلود تصویر هم آن را بالا می‌برد)
        }
        extra?.Invoke(form);
        Assert.Equal(HttpStatusCode.Redirect, (await browser.PostAsync(id is null ? "/Admin/Publications/Edit" : $"/Admin/Publications/Edit?id={id}", form)).StatusCode);
        using var scope = api.Services.CreateScope();
        return (await scope.ServiceProvider.GetRequiredService<AppDbContext>().Publications.AsNoTracking().OrderByDescending(p => p.PublicationId).FirstAsync(p => p.Title == title)).PublicationId;
    }

    private async Task<byte[]?> CoverOf(long id)
    {
        using var scope = api.Services.CreateScope();
        var image = await scope.ServiceProvider.GetRequiredService<AppDbContext>().PublicationImages.AsNoTracking().SingleOrDefaultAsync(i => i.PublicationId == id);
        return image?.Data;
    }

    [Fact]
    public void The_six_default_images_exist_are_png_within_the_upload_limit_and_the_auto_pick_is_stable()
    {
        Assert.Equal(6, Defaults.Names.Count);
        foreach (var bytes in DefaultImageBytes())
        {
            Assert.True(bytes.Length is > 0 and <= PublicationImageRules.MaxBytes);
            Assert.Equal([0x89, 0x50, 0x4E, 0x47], bytes[..4]);                                                       // PNG
        }
        Assert.Equal(6, DefaultImageBytes().Select(b => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(b))).Distinct().Count());   // شش تصویر متفاوت
        Assert.Equal(Defaults.Pick("یک عنوان"), Defaults.Pick("یک عنوان"));                                         // برای یک عنوان همیشه همان
        Assert.All(new[] { "", null, "الف", "خبر مهم", new string('x', 500) }, t => Assert.Contains(Defaults.Pick(t), Defaults.Names));
        Assert.Equal(Defaults.Names.Count, Enumerable.Range(0, 200).Select(i => Defaults.Pick("خبر " + i)).Distinct().Count());   // عنوان‌های مختلف همه‌ی تصویرها را می‌گیرند
        Assert.False(Defaults.IsValid("../port-1"));
        Assert.False(Defaults.IsValid("port-7"));
        Assert.Null(Defaults.PathOf(DefaultImagesDirectory(), "..\\..\\secret"));                                      // مسیر از ورودی ساخته نمی‌شود
    }

    [Fact]
    public async Task A_publication_without_an_image_gets_one_of_the_default_images_automatically()
    {
        var (admin, browser, _, _, _) = await AdminBrowserAsync();
        using (admin) using (browser)
        {
            var id = await SavePublicationPage(browser, "خبر بدون تصویر");
            var cover = await CoverOf(id);
            Assert.NotNull(cover);
            Assert.Contains(DefaultImageBytes(), b => b.SequenceEqual(cover!));                                        // یکی از شش تصویر پیش‌فرض
            await Login();
            Assert.NotEqual(0, (await client.GetFromJsonAsync<PublicationDto>($"/api/admin/publications/{id}"))!.ImageVersion);
        }
    }

    [Fact]
    public async Task The_chosen_default_wins_without_a_file_an_uploaded_file_wins_over_it_and_an_existing_image_is_kept()
    {
        var (admin, browser, _, _, _) = await AdminBrowserAsync();
        using (admin) using (browser)
        {
            var chosen = await SavePublicationPage(browser, "خبر با انتخاب", f => f.Add(new StringContent("port-4"), "DefaultImage"));
            Assert.True(DefaultImageBytes()[3].SequenceEqual((await CoverOf(chosen))!));                              // همان port-4

            var upload = await SavePublicationPage(browser, "خبر با فایل", f =>
            {
                f.Add(new StringContent("port-2"), "DefaultImage");
                var file = new ByteArrayContent(TinyPng); file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
                f.Add(file, "ImageFile", "mine.png");
            });
            Assert.True(TinyPng.SequenceEqual((await CoverOf(upload))!));                                              // فایل بارگذاری‌شده، نه پیش‌فرض

            var before = await CoverOf(chosen);
            await SavePublicationPage(browser, "خبر با انتخاب", f => f.Add(new StringContent(""), "DefaultImage"), id: chosen);   // ویرایش بدون تغییر تصویر
            Assert.True(before!.SequenceEqual((await CoverOf(chosen))!));                                              // تصویر فعلی می‌ماند

            await SavePublicationPage(browser, "خبر با انتخاب", f => f.Add(new StringContent("port-6"), "DefaultImage"), id: chosen);   // انتخاب صریح، جایگزین می‌شود
            Assert.True(DefaultImageBytes()[5].SequenceEqual((await CoverOf(chosen))!));
        }
    }

    [Fact]
    public async Task Removing_the_image_replaces_it_with_a_default_and_an_invalid_default_name_is_ignored()
    {
        var (admin, browser, _, _, _) = await AdminBrowserAsync();
        using (admin) using (browser)
        {
            var id = await SavePublicationPage(browser, "خبر با حذف تصویر", f =>
            {
                var file = new ByteArrayContent(TinyPng); file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
                f.Add(file, "ImageFile", "mine.png");
            });
            Assert.True(TinyPng.SequenceEqual((await CoverOf(id))!));
            await SavePublicationPage(browser, "خبر با حذف تصویر", f => f.Add(new StringContent("true"), "RemoveImage"), id: id);
            var replaced = await CoverOf(id);
            Assert.NotNull(replaced);                                                                                  // بدون تصویر نمی‌ماند
            Assert.Contains(DefaultImageBytes(), b => b.SequenceEqual(replaced!));

            var hostile = await SavePublicationPage(browser, "خبر با نام بد", f => f.Add(new StringContent("../../appsettings"), "DefaultImage"));
            var hostileCover = await CoverOf(hostile);
            Assert.Contains(DefaultImageBytes(), b => b.SequenceEqual(hostileCover!));                    // نام نامعتبر نادیده و خودکار انتخاب شد
        }
    }

    [Fact]
    public async Task The_form_shows_the_default_images_next_to_the_file_input()
    {
        var (admin, browser, _, _, _) = await AdminBrowserAsync();
        using (admin) using (browser)
        {
            var html = WebUtility.HtmlDecode(await browser.GetStringAsync("/Admin/Publications/Edit"));
            Assert.Contains("یکی از تصاویر پیش‌فرض را انتخاب کنید", html);
            Assert.Equal(7, System.Text.RegularExpressions.Regex.Matches(html, "name=\"DefaultImage\"").Count);      // «خودکار» + شش تصویر
            foreach (var name in Defaults.Names) Assert.Contains($"/assets/images/publication-defaults/{name}.png", html);
            Assert.Contains("خودکار انتخاب می‌شود", html);
        }
    }

    [Fact]
    public async Task An_inactive_home_application_still_sends_and_shows_administrator_notifications()
    {
        EnsureHomeApplication();
        using (var scope = api.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Applications.Single(a => a.ApplicationCode == "INTERANET").IsActive = false; db.SaveChanges();           // مثل migration 019
        }
        await Login();
        Assert.Equal(HttpStatusCode.OK, (await SendNotification(AdminNotificationAudience.All)).StatusCode);
        Assert.Single(await MemberNotifications());                                                                    // هنوز به کاربر می‌رسد
        await Login("member");
        Assert.DoesNotContain("اینترانت", (await client.GetFromJsonAsync<List<PortalApplicationDto>>("/api/portal/applications"))!.Select(a => a.Name));   // کارتش دیده نمی‌شود
        await Login();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/admin/dashboard")).StatusCode);                  // مجوزهای پنل سالم است
    }
}
