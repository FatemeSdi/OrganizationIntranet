extern alias PortalEntry;
extern alias AdminEntry;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using OrganizationIntranet.Api.Data;
using OrganizationIntranet.Contracts;
using Xunit;
using AppEntity = OrganizationIntranet.Domain.Entities.Application;

namespace OrganizationIntranet.Tests;

// پرتال عمومی: بازدیدکننده‌ی ناشناس فقط سامانه‌ها و مطالب «منتشرشده و عمومی» را می‌بیند؛ هیچ داده‌ی خصوصی، پیش‌نویس یا رابط کاربری واردشده نشت نمی‌کند.
public sealed partial class PortalSmokeTests
{
    private const string PublicAppName = "درگاه عمومی آزمایشی";
    private const string PrivateAppName = "سامانه خصوصی آزمایشی";

    private HttpClient Anonymous(bool clientKey = true)
    {
        var anonymous = api.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        if (clientKey) anonymous.DefaultRequestHeaders.Add("X-Intranet-Client", ApiFactory.ClientKey);
        return anonymous;
    }
    private void SeedApplications()
    {
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        AppEntity Make(string name, string code, bool isPublic, bool active, string? url = "https://portal.example.org/app") =>
            new() { ApplicationName = name, ApplicationCode = code, BaseUrl = url, Description = "توضیح", Icon = "mail", IsPublic = isPublic, IsActive = active, RequiresLogin = false };
        db.Applications.AddRange(
            Make(PublicAppName, "PUB-OK", true, true),
            Make(PrivateAppName, "PRIV", false, true),
            Make("سامانه عمومی غیرفعال", "PUB-OFF", true, false),
            Make("سامانه با آدرس ناامن", "PUB-BAD", true, true, "javascript:alert(1)"));
        db.SaveChanges();
    }
    private async Task<(PublicationDto Draft, PublicationDto Internal, PublicationDto Public, PublicationDto Circular)> SeedPublicationsAsync()
    {
        await Login(); // مدیر
        async Task<PublicationDto> Add(string title, string kind, bool published, bool isPublic)
        {
            var response = await client.PostAsJsonAsync("/api/admin/publications", new PublicationEditRequest { Title = title, Summary = "خلاصه " + title, Body = "متن کامل " + title, Kind = kind, IsPublished = published, IsPublic = isPublic });
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<PublicationDto>())!;
        }
        return (await Add("پیش‌نویس محرمانه", "News", false, false), await Add("اطلاعیه داخلی", "Announcement", true, false),
            await Add("<script>alert(1)</script> خبر عمومی", "News", true, true), await Add("بخشنامه عمومی", "Circular", true, true));
    }

    [Fact]
    public async Task Anonymous_API_lists_only_active_public_applications_and_exposes_no_private_fields()
    {
        SeedApplications();
        using var visitor = Anonymous();
        var text = await visitor.GetStringAsync("/api/public/applications");
        var apps = JsonSerializer.Deserialize<List<PublicApplicationDto>>(text, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal([PublicAppName, "سامانه با آدرس ناامن"], apps.Select(a => a.Name).Order().ToArray());
        Assert.Null(apps.Single(a => a.Name == "سامانه با آدرس ناامن").Url); // URL غیر HTTP(S) هرگز به مرورگر نمی‌رسد
        Assert.Equal("https://portal.example.org/app", apps.Single(a => a.Name == PublicAppName).Url);
        Assert.DoesNotContain(PrivateAppName, text);
        Assert.DoesNotContain("غیرفعال", text);
        foreach (var hidden in new[] { "unreadCount", "services", "applicationCode", "isPublic", "displayOrder" }) Assert.DoesNotContain(hidden, text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Anonymous_API_lists_only_published_public_publications_and_hides_drafts_and_internal_items()
    {
        var (draft, internalItem, publicNews, circular) = await SeedPublicationsAsync();
        using var visitor = Anonymous();
        var list = (await visitor.GetFromJsonAsync<PublicationListDto>("/api/public/publications"))!;
        Assert.Equal(2, list.Total);
        Assert.Equal(new[] { circular.PublicationId, publicNews.PublicationId }.Order().ToArray(), list.Items.Select(i => i.PublicationId).Order().ToArray());
        Assert.All(list.Items, i => { Assert.Equal("", i.Body); Assert.True(i.IsPublic); });
        Assert.Single((await visitor.GetFromJsonAsync<PublicationListDto>("/api/public/publications?kind=Circular"))!.Items);
        Assert.Single((await visitor.GetFromJsonAsync<PublicationListDto>("/api/public/publications?search=" + Uri.EscapeDataString("بخشنامه")))!.Items);
        Assert.Empty((await visitor.GetFromJsonAsync<PublicationListDto>("/api/public/publications?kind=Announcement"))!.Items); // اطلاعیه‌ی داخلی دیده نمی‌شود
        Assert.Equal(HttpStatusCode.BadRequest, (await visitor.GetAsync("/api/public/publications?page=0")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await visitor.GetAsync("/api/public/publications?kind=Hacked")).StatusCode);

        var detail = (await visitor.GetFromJsonAsync<PublicationDto>($"/api/public/publications/{circular.PublicationId}"))!;
        Assert.Contains("متن کامل", detail.Body);
        foreach (var hidden in new[] { draft.PublicationId, internalItem.PublicationId, 99999 }) // وجود پیش‌نویس/داخلی لو نمی‌رود: همه ۴۰۴
            Assert.Equal(HttpStatusCode.NotFound, (await visitor.GetAsync($"/api/public/publications/{hidden}")).StatusCode);
    }

    [Fact]
    public async Task Anonymous_visitors_cannot_reach_private_APIs_and_the_internal_client_key_is_still_required()
    {
        using var visitor = Anonymous();
        foreach (var path in new[] { "/api/publications", "/api/publications/1", "/api/portal/applications", "/api/portal/notifications", "/api/account/me", "/api/admin/publications" })
            Assert.Equal(HttpStatusCode.Unauthorized, (await visitor.GetAsync(path)).StatusCode);
        using var noKey = Anonymous(clientKey: false);
        foreach (var path in new[] { "/api/public/applications", "/api/public/publications", "/api/public/publications/1" })
            Assert.Equal(HttpStatusCode.Unauthorized, (await noKey.GetAsync(path)).StatusCode);
        var post = (await visitor.PostAsJsonAsync("/api/public/publications", new { })).StatusCode; // فقط خواندنی
        Assert.True(post is HttpStatusCode.Unauthorized or HttpStatusCode.MethodNotAllowed, $"POST returned {(int)post}");
    }

    [Fact]
    public async Task Public_flag_requires_publication_is_round_tripped_and_cannot_survive_unpublishing()
    {
        await Login();
        var request = new PublicationEditRequest { Title = "عنوان", Summary = "خلاصه", Body = "متن", Kind = "News", IsPublished = false, IsPublic = true };
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/admin/publications", request)).StatusCode); // پیش‌نویسِ عمومی ممنوع
        request.IsPublished = true;
        var created = (await (await client.PostAsJsonAsync("/api/admin/publications", request)).Content.ReadFromJsonAsync<PublicationDto>())!;
        Assert.True(created.IsPublic);
        Assert.True((await client.GetFromJsonAsync<PublicationDto>($"/api/admin/publications/{created.PublicationId}"))!.IsPublic);
        request.Revision = created.Revision; request.IsPublished = false; // برداشتن انتشار بدون برداشتن «عمومی»
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/api/admin/publications/{created.PublicationId}", request)).StatusCode);
        request.IsPublic = false;
        var draft = (await (await client.PostAsJsonAsync($"/api/admin/publications/{created.PublicationId}", request)).Content.ReadFromJsonAsync<PublicationDto>())!;
        Assert.False(draft.IsPublic);
        using var visitor = Anonymous();
        Assert.Equal(HttpStatusCode.NotFound, (await visitor.GetAsync($"/api/public/publications/{created.PublicationId}")).StatusCode);
    }

    [Fact]
    public async Task Anonymous_portal_home_shows_only_public_content_and_no_signed_in_interface()
    {
        SeedApplications();
        var (draft, internalItem, _, circular) = await SeedPublicationsAsync();
        using var portal = Ui<PortalEntry::Program>("Portal");
        using var visitor = portal.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var home = await visitor.GetAsync("/");
        Assert.Equal(HttpStatusCode.Redirect, home.StatusCode);
        Assert.Equal("/Portal", home.Headers.Location!.OriginalString);

        var html = await visitor.GetStringAsync("/Portal");
        var text = WebUtility.HtmlDecode(html); // Razor کدگذاری موجودیتی متن پویای فارسی را انجام می‌دهد
        Assert.Contains(PublicAppName, text);
        Assert.Contains("href=\"https://portal.example.org/app\"", html);
        Assert.Contains("بخشنامه عمومی", text);
        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", html); // متن مطلب encode می‌شود
        Assert.DoesNotContain("<script>alert(1)</script>", html);
        Assert.DoesNotContain("javascript:alert(1)", html);
        Assert.DoesNotContain(PrivateAppName, text);
        Assert.DoesNotContain("پیش‌نویس محرمانه", text);
        Assert.DoesNotContain("اطلاعیه داخلی", text);
        Assert.Contains("/Account/Login?returnUrl=%2FPortal", html);
        foreach (var signedInOnly in new[] { "id=\"access-requests\"", "id=\"bellBtn\"", "id=\"userBtn\"", "id=\"supervision\"", "/Account/Logout", "name=\"__RequestVerificationToken\"" })
            Assert.DoesNotContain(signedInOnly, html);
        Assert.NotEqual(draft.PublicationId, internalItem.PublicationId); // هر دو آزمایش شدند

        using var admin = Ui<AdminEntry::Program>("Admin");
        using var adminVisitor = admin.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        Assert.Equal("/Account/Login", (await adminVisitor.GetAsync("/")).Headers.Location!.OriginalString); // پنل مدیریت همچنان با ورود شروع می‌شود
        Assert.Equal(HttpStatusCode.Redirect, (await adminVisitor.GetAsync("/Admin")).StatusCode);
        Assert.NotNull(circular);
    }

    [Fact]
    public async Task Anonymous_publication_pages_show_public_items_only()
    {
        var (draft, internalItem, _, circular) = await SeedPublicationsAsync();
        using var portal = Ui<PortalEntry::Program>("Portal");
        using var visitor = portal.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var list = WebUtility.HtmlDecode(await visitor.GetStringAsync("/Publications"));
        Assert.Contains("بخشنامه عمومی", list);
        Assert.DoesNotContain("اطلاعیه داخلی", list);
        Assert.DoesNotContain("پیش‌نویس محرمانه", list);
        Assert.Contains("متن کامل بخشنامه عمومی", WebUtility.HtmlDecode(await visitor.GetStringAsync($"/Publications/Details/{circular.PublicationId}")));
        Assert.Equal(HttpStatusCode.NotFound, (await visitor.GetAsync($"/Publications/Details/{internalItem.PublicationId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await visitor.GetAsync($"/Publications/Details/{draft.PublicationId}")).StatusCode);
    }

    [Fact]
    public async Task Signed_in_users_still_get_internal_publications_and_the_signed_in_interface()
    {
        var (_, _, _, _) = await SeedPublicationsAsync();
        var token = await Login("member");
        using var portal = Ui<PortalEntry::Program>("Portal");
        using var member = portal.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        member.DefaultRequestHeaders.Add("Cookie", Cookie(portal.Services, token, false));
        var html = await member.GetStringAsync("/Portal");
        var text = WebUtility.HtmlDecode(html);
        Assert.Contains("اطلاعیه داخلی", text); // منتشرشده‌ی غیرعمومی فقط برای کاربر واردشده
        Assert.DoesNotContain("پیش‌نویس محرمانه", text);
        Assert.Contains("id=\"userBtn\"", html);
        Assert.Contains("/Account/Logout", html);
        Assert.DoesNotContain("class=\"login-btn\"", html);
        Assert.Contains("اطلاعیه داخلی", WebUtility.HtmlDecode(await member.GetStringAsync("/Publications")));
    }

    [Fact]
    public async Task Anonymous_post_handlers_are_rejected_without_calling_the_API()
    {
        using var portal = Ui<PortalEntry::Program>("Portal");
        using var visitor = portal.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        foreach (var handler in new[] { "ReadNotifications", "RequestAccess", "OpenNotification", "CancelRequest", "ReadSupervisionAlert" })
        {
            var status = (await visitor.PostAsync($"/Portal?handler={handler}", new FormUrlEncodedContent(new Dictionary<string, string>()))).StatusCode;
            Assert.True(status is HttpStatusCode.BadRequest or HttpStatusCode.Redirect or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden, $"{handler} returned {(int)status}");
        }
    }
}
