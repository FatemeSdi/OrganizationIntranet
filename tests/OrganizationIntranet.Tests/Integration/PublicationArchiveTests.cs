extern alias AdminEntry;
using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using OrganizationIntranet.Api.Data;
using OrganizationIntranet.Contracts;
using Xunit;

namespace OrganizationIntranet.Tests;

// بایگانی (حذف نرم) مطالب: از پرتال و فهرست پیش‌فرض پنهان می‌شود، از انتشار درمی‌آید، قابل بازگردانی است و ویرایش نمی‌شود.
public sealed partial class PortalSmokeTests
{
    private async Task<PublicationDto> CreatePublicationAsync(string title, bool published = true, bool isPublic = true) =>
        (await (await client.PostAsJsonAsync("/api/admin/publications", new PublicationEditRequest { Title = title, Summary = "خلاصه", Body = "متن", Kind = "News", IsPublished = published, IsPublic = isPublic })).Content.ReadFromJsonAsync<PublicationDto>())!;

    [Fact]
    public async Task Archiving_hides_a_publication_everywhere_and_restoring_returns_it_as_a_draft()
    {
        await Login();
        var item = await CreatePublicationAsync("خبر برای بایگانی");
        var other = await CreatePublicationAsync("خبر باقی‌مانده");
        using var visitor = Anonymous();
        Assert.Equal(2, (await visitor.GetFromJsonAsync<PublicationListDto>("/api/public/publications"))!.Total);

        var archived = (await (await client.PostAsync($"/api/admin/publications/{item.PublicationId}/archive", null)).Content.ReadFromJsonAsync<PublicationDto>())!;
        Assert.True(archived.IsArchived);
        Assert.False(archived.IsPublished);                                                                    // بایگانی مطلب را از انتشار درمی‌آورد
        Assert.False(archived.IsPublic);

        Assert.Equal(["خبر باقی‌مانده"], (await visitor.GetFromJsonAsync<PublicationListDto>("/api/public/publications"))!.Items.Select(p => p.Title));
        Assert.Equal(HttpStatusCode.NotFound, (await visitor.GetAsync($"/api/public/publications/{item.PublicationId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/publications/{item.PublicationId}")).StatusCode);   // کاربر واردشده هم نمی‌بیند

        Assert.Equal(["خبر باقی‌مانده"], (await client.GetFromJsonAsync<PublicationListDto>("/api/admin/publications"))!.Items.Select(p => p.Title)); // فهرست پیش‌فرض مدیر
        Assert.Empty((await client.GetFromJsonAsync<PublicationListDto>("/api/admin/publications?status=draft"))!.Items);                         // پیش‌نویس هم نیست
        Assert.Equal(["خبر برای بایگانی"], (await client.GetFromJsonAsync<PublicationListDto>("/api/admin/publications?status=archived"))!.Items.Select(p => p.Title));
        Assert.True((await client.GetFromJsonAsync<PublicationDto>($"/api/admin/publications/{item.PublicationId}"))!.IsArchived);   // مدیر با شناسه می‌بیند

        var again = await client.PostAsync($"/api/admin/publications/{item.PublicationId}/archive", null);                // تکرار بی‌اثر
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);

        var restored = (await (await client.PostAsync($"/api/admin/publications/{item.PublicationId}/restore", null)).Content.ReadFromJsonAsync<PublicationDto>())!;
        Assert.False(restored.IsArchived);
        Assert.False(restored.IsPublished);                                                                    // پیش‌نویس برمی‌گردد، منتشر نمی‌شود
        Assert.Equal(["خبر برای بایگانی"], (await client.GetFromJsonAsync<PublicationListDto>("/api/admin/publications?status=draft"))!.Items.Select(p => p.Title));
        Assert.Equal(1, (await visitor.GetFromJsonAsync<PublicationListDto>("/api/public/publications"))!.Total);
        _ = other;
    }

    [Fact]
    public async Task Archived_publications_cannot_be_edited_and_unknown_ids_are_not_found()
    {
        await Login();
        var item = await CreatePublicationAsync("بایگانی و ویرایش", published: false, isPublic: false);
        (await client.PostAsync($"/api/admin/publications/{item.PublicationId}/archive", null)).EnsureSuccessStatusCode();
        var current = (await client.GetFromJsonAsync<PublicationDto>($"/api/admin/publications/{item.PublicationId}"))!;

        var edit = await client.PostAsJsonAsync($"/api/admin/publications/{item.PublicationId}",
            new PublicationEditRequest { Title = "x", Summary = "x", Body = "x", Kind = "News", IsPublished = true, Revision = current.Revision });
        Assert.Equal(HttpStatusCode.BadRequest, edit.StatusCode);                                              // ابتدا باید بازگردانی شود
        Assert.Contains("بازگردانی", (await edit.Content.ReadFromJsonAsync<OperationResult>())!.Message);

        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync("/api/admin/publications/999999/archive", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync("/api/admin/publications/999999/restore", null)).StatusCode);
    }

    [Fact]
    public async Task Database_forbids_a_published_archived_publication()
    {
        await Login();
        var item = await CreatePublicationAsync("قید بانک");
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var entity = db.Publications.Single(p => p.PublicationId == item.PublicationId);
        entity.IsArchived = true;                                                                              // هنوز منتشرشده است
        Assert.ThrowsAny<Exception>(() => db.SaveChanges());                                                   // CK_Publication_ArchivedNotPublished
    }

    [Fact]
    public async Task Admin_pages_offer_archive_and_restore_with_a_confirmation()
    {
        var (admin, browser, _, _, _) = await AdminBrowserAsync();
        using (admin) using (browser)
        {
            var item = await CreatePublicationAsync("صفحه‌ی بایگانی");
            var edit = WebUtility.HtmlDecode(await browser.GetStringAsync($"/Admin/Publications/Edit?id={item.PublicationId}"));
            Assert.Contains("data-confirm=\"«صفحه‌ی بایگانی» بایگانی شود؟", edit);
            Assert.Matches(@"handler=Archive", edit);
            Assert.DoesNotContain("handler=Restore", edit);

            (await client.PostAsync($"/api/admin/publications/{item.PublicationId}/archive", null)).EnsureSuccessStatusCode();
            var archivedEdit = WebUtility.HtmlDecode(await browser.GetStringAsync($"/Admin/Publications/Edit?id={item.PublicationId}"));
            Assert.Contains("این مطلب بایگانی شده است", archivedEdit);
            Assert.Contains("handler=Restore", archivedEdit);
            Assert.DoesNotContain("ti-device-floppy", archivedEdit);                                           // ذخیره پنهان است

            var list = WebUtility.HtmlDecode(await browser.GetStringAsync("/Admin/Publications?status=archived"));
            Assert.Contains("صفحه‌ی بایگانی", list);
            Assert.Contains("بازگردانی", list);
            Assert.Contains("<option value=\"archived\" selected=\"selected\">بایگانی‌شده</option>", list);
            Assert.DoesNotContain("صفحه‌ی بایگانی", WebUtility.HtmlDecode(await browser.GetStringAsync("/Admin/Publications")));   // فهرست پیش‌فرض
        }
    }
}
