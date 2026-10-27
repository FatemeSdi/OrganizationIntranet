extern alias AdminEntry;
using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using OrganizationIntranet.Api.Data;
using OrganizationIntranet.Contracts;
using OrganizationIntranet.Domain.Entities;
using Xunit;

namespace OrganizationIntranet.Tests;

// فهرست اطلاع‌رسانی پنل مدیریت: جدول فشرده با صفحه‌بندی، فیلتر وضعیت و سقف طول فیلدها در همه‌ی لایه‌ها.
public sealed partial class PortalSmokeTests
{
    private void SeedPublications(long author, int published, int drafts, int publicOnes, string summary = "خلاصه")
    {
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTime.UtcNow;
        Publication New(string title, bool isPublished, bool isPublic) => new()
        {
            Title = title, Summary = summary, Body = "B", Kind = "Announcement", IsPublished = isPublished, IsPublic = isPublic,
            PublishedAt = isPublished ? now : null, CreatedBy = author, UpdatedBy = author, CreatedAt = now, UpdatedAt = now,
        };
        for (var i = 0; i < published; i++) db.Publications.Add(New($"منتشرشده {i}", true, false));
        for (var i = 0; i < drafts; i++) db.Publications.Add(New($"پیش‌نویس {i}", false, false));
        for (var i = 0; i < publicOnes; i++) db.Publications.Add(New($"عمومی {i}", true, true));
        db.SaveChanges();
    }

    [Fact]
    public async Task Admin_publication_list_is_a_compact_paged_table_with_a_status_filter()
    {
        var (admin, browser, _, memberId, _) = await AdminBrowserAsync();
        using (admin) using (browser)
        {
            SeedPublications(memberId, 22, 3, 2, summary: new string('ب', 1000));
            var page1 = WebUtility.HtmlDecode(await browser.GetStringAsync("/Admin/Publications"));
            Assert.Contains("class=\"table table-admin publication-table\"", page1);              // جدول، نه کارت‌های بلند
            Assert.DoesNotContain("class=\"item-card", page1);
            Assert.Equal(20, Regex.Matches(page1, "class=\"publication-row__title\"").Count);      // ۲۰ مطلب در هر صفحه
            Assert.Contains("class=\"publication-row__summary\"", page1);                          // خلاصه با CSS دوخطی می‌شود
            Assert.Contains("27 مطلب", page1);
            Assert.Contains("href=\"/Admin/Publications?pageNumber=2\"", page1);

            var page2 = WebUtility.HtmlDecode(await browser.GetStringAsync("/Admin/Publications?pageNumber=2"));
            Assert.Equal(7, Regex.Matches(page2, "class=\"publication-row__title\"").Count);

            var drafts = WebUtility.HtmlDecode(await browser.GetStringAsync("/Admin/Publications?status=draft"));
            Assert.Equal(3, Regex.Matches(drafts, "class=\"publication-row__title\"").Count);
            Assert.DoesNotContain("منتشرشده 0<", drafts);
            Assert.Contains("<option value=\"draft\" selected=\"selected\">پیش‌نویس</option>", drafts);
            Assert.Contains("پاک‌کردن فیلترها", drafts);

            var publicOnes = WebUtility.HtmlDecode(await browser.GetStringAsync("/Admin/Publications?status=public"));
            Assert.Equal(2, Regex.Matches(publicOnes, "class=\"publication-row__title\"").Count);
            var published = WebUtility.HtmlDecode(await browser.GetStringAsync("/Admin/Publications?status=published&pageNumber=2"));
            Assert.Equal(4, Regex.Matches(published, "class=\"publication-row__title\"").Count);     // ۲۲ + ۲ عمومی = ۲۴ منتشرشده
            Assert.Contains("href=\"/Admin/Publications?status=published&pageNumber=1\"", published); // صفحه‌بندی فیلتر را نگه می‌دارد

            Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync("/Admin/Publications?status=bogus")).StatusCode); // مقدار نامعتبر = همه

            var css = await browser.GetStringAsync("/assets/css/admin-ui.css");
            Assert.Contains("-webkit-line-clamp: 2", css[css.IndexOf(".publication-row__summary", StringComparison.Ordinal)..]);
        }
    }

    [Fact]
    public async Task Admin_publication_api_rejects_an_unknown_status_filter()
    {
        await Login();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/admin/publications?status=bogus")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/admin/publications?status=draft")).StatusCode);
    }

    [Theory]
    [InlineData(200, 1000, 50000, true)]
    [InlineData(201, 10, 10, false)]
    [InlineData(10, 1001, 10, false)]
    [InlineData(10, 10, 50001, false)]
    public async Task Publication_field_lengths_match_the_database_columns(int title, int summary, int body, bool accepted)
    {
        await Login();
        var response = await client.PostAsJsonAsync("/api/admin/publications", new PublicationEditRequest { Title = new string('ع', title), Summary = new string('خ', summary), Body = new string('م', body), Kind = "News" });
        Assert.Equal(accepted ? HttpStatusCode.OK : HttpStatusCode.BadRequest, response.StatusCode);

        using var scope = api.Services.CreateScope();
        var column = scope.ServiceProvider.GetRequiredService<AppDbContext>().Model.FindEntityType(typeof(Publication))!;
        Assert.Equal([200, 1000, 50000], new[] { "Title", "Summary", "Body" }.Select(c => column.FindProperty(c)!.GetMaxLength()!.Value).ToArray()); // nvarchar(200/1000/50000)
    }

    [Fact]
    public async Task Admin_edit_forms_no_longer_show_the_required_field_hint()
    {
        var (admin, browser, _, memberId, _) = await AdminBrowserAsync();
        using (admin) using (browser)
        {
            foreach (var path in new[] { "/Admin/Publications/Edit", "/Admin/Users/Edit", "/Admin/Applications/Edit", "/Admin/Roles/Edit" })
            {
                var response = await browser.GetAsync(path);
                if (response.StatusCode != HttpStatusCode.OK) continue;   // برخی فرم‌ها بدون شناسه به فهرست برمی‌گردند
                Assert.DoesNotContain("فیلد الزامی", WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
            }
            var publication = WebUtility.HtmlDecode(await browser.GetStringAsync("/Admin/Publications/Edit"));
            Assert.DoesNotContain("فیلد الزامی", publication);
            Assert.Contains("maxlength=\"1000\"", publication);                                    // سقف خلاصه در فرم هم اعمال است
        }
    }
}
