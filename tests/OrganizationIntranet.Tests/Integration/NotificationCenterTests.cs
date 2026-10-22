extern alias PortalEntry;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrganizationIntranet.Api.Data;
using OrganizationIntranet.Contracts;
using OrganizationIntranet.Domain.Entities;
using Xunit;
using NC = OrganizationIntranet.Contracts.NotificationCenter;

namespace OrganizationIntranet.Tests;

// مرکز اعلان Portal (صفحه‌بندی و فیلتر سمت سرور روی هر دو منبع) و کارتابل نظارت با تب وضعیت و فیلتر سامانه.
public sealed partial class PortalSmokeTests
{
    /// <summary>
    /// برای admin (بررسی‌کننده‌ی همه‌ی سامانه‌ها): ۲۵ اعلان سامانه (۲۰ تازه که ۳تایش خوانده‌شده، ۵ قدیمی ۴۰روزه)
    /// و ۲ هشدار درخواست دسترسی از member (یکی تازه و خوانده‌نشده و Pending، یکی ۱۰روزه و خوانده‌شده و Rejected).
    /// </summary>
    private int SeedNotificationCenter()
    {
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var admin = db.Users.Single(u => u.Username == "admin"); var member = db.Users.Single(u => u.Username == "member");
        var app = db.Applications.Include(a => a.Services).Single(); app.IsPublic = true;
        app.Services.Add(new() { ServiceCode = "NOTIFICATIONS", DisplayName = "اعلان" });
        var now = DateTime.UtcNow;
        for (var i = 0; i < 25; i++)
        {
            var at = i < 20 ? now.AddMinutes(-i) : now.AddDays(-40).AddMinutes(-i);
            db.UserNotifications.Add(new UserNotification { UserId = admin.UserId, CreatedAt = at, IsRead = i is 1 or 2 or 3, ReadAt = i is 1 or 2 or 3 ? now : null,
                Notification = new() { Application = app, Title = $"اعلان شماره {i:00}", Message = "متن", CreatedAt = at } });
        }
        // قید بانک: درخواست تصمیم‌گرفته تاریخ و بررسی‌کننده دارد
        ApplicationAccessRequest Request(string status, DateTime at) => new() { Application = app, UserId = member.UserId, Reason = "دلیل " + status, Status = status, CreatedAt = at,
            ResolvedAt = status == AccessRequestStatuses.Pending ? null : at.AddHours(1), ReviewedBy = status == AccessRequestStatuses.Pending ? null : admin.UserId };
        db.AccessRequestAlerts.Add(new AccessRequestAlert { Request = Request(AccessRequestStatuses.Pending, now.AddSeconds(-30)), UserId = admin.UserId, InApp = true, CreatedAt = now.AddSeconds(-30) });
        db.AccessRequestAlerts.Add(new AccessRequestAlert { Request = Request(AccessRequestStatuses.Rejected, now.AddDays(-10)), UserId = admin.UserId, InApp = true, IsRead = true, ReadAt = now, CreatedAt = now.AddDays(-10) });
        db.SaveChanges();
        return app.ApplicationId;
    }

    private async Task<NotificationCenterDto> Center(string query = "") => (await client.GetFromJsonAsync<NotificationCenterDto>("/api/portal/notifications/page" + query))!;

    [Fact]
    public async Task Notification_center_pages_both_sources_on_the_server_newest_first()
    {
        SeedNotificationCenter();
        await Login();
        var first = await Center();
        Assert.Equal((27, 27, 23), (first.Total, first.AllCount, first.UnreadCount)); // بیش از سقف قدیمی ۵۰تایی هم درست شمرده می‌شود
        Assert.Equal(NC.PageSize, first.Items.Count);
        Assert.Equal(2, first.TotalPages);
        Assert.Equal(first.Items.OrderByDescending(i => i.CreatedAt).Select(i => i.Id), first.Items.Select(i => i.Id));
        Assert.Equal(NC.SystemKind, first.Items[0].Kind);                                   // «اعلان شماره 00» تازه‌ترین است
        Assert.Equal(NC.RequestKind, first.Items[1].Kind);                                  // هشدار ۳۰ ثانیه پیش بین اعلان‌ها
        Assert.Equal("درخواست دسترسی جدید از نام آزمایشی", first.Items[1].Title);
        var second = await Center("?page=2");
        Assert.Equal(7, second.Items.Count);
        Assert.Empty(first.Items.Select(i => (i.Kind, i.Id)).Intersect(second.Items.Select(i => (i.Kind, i.Id))));
        Assert.Empty((await Center("?page=3")).Items);

        var unread = await Center("?status=unread");
        Assert.Equal((23, 27), (unread.Total, unread.AllCount));
        Assert.All(unread.Items, i => Assert.True(i.Unread));
        Assert.Equal(4, (await Center("?status=read")).Total);
    }

    [Fact]
    public async Task Notification_center_filters_by_source_and_period_and_rejects_invalid_filters()
    {
        SeedNotificationCenter();
        await Login();
        var requests = await Center("?kind=request");
        Assert.Equal((2, 1), (requests.Total, requests.UnreadCount));
        Assert.All(requests.Items, i => Assert.Equal(NC.RequestKind, i.Kind));
        Assert.Equal(25, (await Center("?kind=system")).Total);
        var week = await Center("?days=7");
        Assert.Equal(21, week.Total);                                                        // ۲۰ اعلان تازه + هشدار تازه
        Assert.Equal(20, (await Center("?days=7&kind=system")).Total);
        Assert.Equal(22, (await Center("?days=30")).Total);                                  // هشدار ۱۰روزه هم
        Assert.Equal(1, (await Center("?days=30&kind=request&status=read")).Total);

        foreach (var bad in new[] { "?kind=sms", "?status=new", "?days=5", "?page=0", "?page=100001" })
            Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/portal/notifications/page" + bad)).StatusCode);
    }

    [Fact]
    public async Task Notification_center_shows_only_the_callers_own_visible_items()
    {
        SeedNotificationCenter();
        await Login("member");
        var mine = await Center();
        Assert.Equal((0, 0), (mine.AllCount, mine.Items.Count));                            // اعلان و هشدار admin دیده نمی‌شود

        // سرویس اعلان سامانه خاموش شود: اعلان‌هایش مثل فهرست قبلی پنهان می‌شوند، هشدارها می‌مانند
        using (var scope = api.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            foreach (var s in db.Set<ApplicationServiceRegistration>()) s.IsActive = false;
            db.SaveChanges();
        }
        await Login();
        var center = await Center();
        Assert.Equal(2, center.AllCount);
        Assert.All(center.Items, i => Assert.Equal(NC.RequestKind, i.Kind));
    }

    [Fact]
    public async Task Supervision_inbox_filters_by_status_and_application_on_the_server()
    {
        var appId = SeedNotificationCenter();
        await Login();
        var summary = (await client.GetFromJsonAsync<SupervisionSummaryDto>("/api/portal/supervision/inbox/summary"))!;
        Assert.True(summary.IsSupervisor);
        Assert.Contains(summary.Applications, a => a.ApplicationId == appId);
        Assert.Equal((1, 1, 2), (summary.Count(AccessRequestStatuses.Pending), summary.Count(AccessRequestStatuses.Rejected), summary.Count(null)));

        var rejected = (await client.GetFromJsonAsync<AccessRequestListDto>($"/api/portal/supervision/inbox?status=Rejected&applicationId={appId}"))!;
        Assert.Equal("دلیل Rejected", Assert.Single(rejected.Items).Reason);
        Assert.Empty((await client.GetFromJsonAsync<AccessRequestListDto>("/api/portal/supervision/inbox?applicationId=999"))!.Items);
        Assert.Equal(0, (await client.GetFromJsonAsync<SupervisionSummaryDto>("/api/portal/supervision/inbox/summary?applicationId=999"))!.Count(null));
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/portal/supervision/inbox?status=Unknown")).StatusCode);

        await Login("member");                                                             // ناظر نیست
        Assert.False((await client.GetFromJsonAsync<SupervisionSummaryDto>("/api/portal/supervision/inbox/summary"))!.IsSupervisor);
        Assert.Empty((await client.GetFromJsonAsync<AccessRequestListDto>($"/api/portal/supervision/inbox?applicationId={appId}"))!.Items);
    }

    [Fact]
    public async Task Portal_notifications_page_keeps_filters_across_pages()
    {
        SeedNotificationCenter();
        var token = await Login();
        using var portal = Ui<PortalEntry::Program>("Portal");
        using var browser = portal.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        browser.DefaultRequestHeaders.Add("Cookie", Cookie(portal.Services, token, true));

        var page1 = WebUtility.HtmlDecode(await browser.GetStringAsync("/Notifications?source=system"));
        Assert.Contains("صفحه 1 از 2", page1);
        Assert.Contains("href=\"/Notifications?source=system&pageNumber=2\"", page1);           // صفحه‌ی بعد فیلتر را نگه می‌دارد
        Assert.Contains("href=\"/Notifications?source=system&days=7\"", page1);                 // تغییر بازه فیلتر منبع را نگه می‌دارد
        Assert.DoesNotContain("درخواست دسترسی جدید از", page1);
        var page2 = WebUtility.HtmlDecode(await browser.GetStringAsync("/Notifications?source=system&pageNumber=2"));
        Assert.Contains("اعلان شماره 24", page2);
        Assert.DoesNotContain("اعلان شماره 00", page2);

        var week = WebUtility.HtmlDecode(await browser.GetStringAsync("/Notifications?days=7"));
        Assert.Matches("aria-current=\"true\"[^>]*>۷ روز اخیر", week);
        Assert.DoesNotContain("اعلان شماره 24", week);
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync("/Notifications?days=5&tab=x&source=y&pageNumber=-3")).StatusCode); // پارامتر نامعتبر = پیش‌فرض
        Assert.Contains("اعلانی وجود ندارد", WebUtility.HtmlDecode(await browser.GetStringAsync("/Notifications?days=1&source=request&tab=read")));
    }

    [Fact]
    public async Task Supervision_page_replaces_the_home_inbox_and_is_linked_only_for_supervisors()
    {
        var appId = SeedNotificationCenter();
        var token = await Login();
        using var portal = Ui<PortalEntry::Program>("Portal");
        using var browser = portal.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        browser.DefaultRequestHeaders.Add("Cookie", Cookie(portal.Services, token, true));

        var page = WebUtility.HtmlDecode(await browser.GetStringAsync("/Supervision"));
        Assert.Contains("href=\"/Supervision?status=Pending\"", page);
        Assert.Contains("دلیل Pending", page); Assert.Contains("دلیل Rejected", page);
        Assert.Contains("href=\"/Supervision\" aria-current=\"page\"", page);                  // منوی حساب
        var pending = WebUtility.HtmlDecode(await browser.GetStringAsync("/Supervision?status=Pending"));
        Assert.Contains("دلیل Pending", pending); Assert.DoesNotContain("دلیل Rejected", pending);
        Assert.Contains("دلیل Rejected", WebUtility.HtmlDecode(await browser.GetStringAsync($"/Supervision?status=Rejected&app={appId}")));
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync("/Supervision?status=Bad&app=999&pageNumber=0")).StatusCode);

        var home = WebUtility.HtmlDecode(await browser.GetStringAsync("/Portal"));
        Assert.Contains("id=\"supervision-title\">کارتابل نظارت", home);
        Assert.Contains("href=\"/Supervision?status=Pending\"", home);
        Assert.DoesNotContain("دلیل Pending", home);                                           // فهرست کامل دیگر در صفحه‌ی اصلی نیست

        // هشدار از صفحه‌ی اعلان‌ها به کارتابل جدید می‌رود
        var alertId = (await Center("?kind=request&status=unread")).Items.Single().Id;
        var csrf = await Antiforgery(browser, "/Notifications");
        var open = await browser.PostAsync($"/Notifications?handler=ReadAlert&id={alertId}", new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = csrf }));
        Assert.Equal("/Supervision?status=Pending", open.Headers.Location?.OriginalString);
        Assert.Equal(0, (await Center("?kind=request&status=unread")).Total);

        var memberToken = await Login("member");
        using var other = portal.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        other.DefaultRequestHeaders.Add("Cookie", Cookie(portal.Services, memberToken, false));
        Assert.Contains("شما ناظر هیچ سامانه‌ای نیستید", WebUtility.HtmlDecode(await other.GetStringAsync("/Supervision")));
        var memberHome = WebUtility.HtmlDecode(await other.GetStringAsync("/Portal"));
        Assert.DoesNotContain("href=\"/Supervision", memberHome);
        Assert.DoesNotContain("supervision-title", memberHome);
    }

    [Fact]
    public async Task My_access_requests_status_tabs_filter_on_the_server_not_only_the_current_page()
    {
        using (var scope = api.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var member = db.Users.Single(u => u.Username == "member"); var app = db.Applications.Single();
            var now = DateTime.UtcNow;
            db.ApplicationAccessRequests.Add(new ApplicationAccessRequest { ApplicationId = app.ApplicationId, UserId = member.UserId, Reason = "قدیمی‌ترین ردشده", Status = AccessRequestStatuses.Rejected, CreatedAt = now.AddDays(-30), ResolvedAt = now.AddDays(-29), ReviewedBy = db.Users.Single(u => u.Username == "admin").UserId });
            for (var i = 0; i < 21; i++) // صفحه‌ی اول (۲۰تایی) را پر می‌کند
                db.ApplicationAccessRequests.Add(new ApplicationAccessRequest { ApplicationId = app.ApplicationId, UserId = member.UserId, Reason = $"لغوشده {i}", Status = AccessRequestStatuses.Cancelled, CreatedAt = now.AddMinutes(-i), ResolvedAt = now });
            db.SaveChanges();
        }
        var token = await Login("member");
        using var portal = Ui<PortalEntry::Program>("Portal");
        using var browser = portal.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        browser.DefaultRequestHeaders.Add("Cookie", Cookie(portal.Services, token, false));
        Assert.DoesNotContain("قدیمی‌ترین ردشده", WebUtility.HtmlDecode(await browser.GetStringAsync("/AccessRequests")));
        var rejected = WebUtility.HtmlDecode(await browser.GetStringAsync("/AccessRequests?status=Rejected"));
        Assert.Contains("قدیمی‌ترین ردشده", rejected);
        Assert.DoesNotContain("لغوشده 0", rejected);
        Assert.Contains("درخواستی با این وضعیت ندارید", WebUtility.HtmlDecode(await browser.GetStringAsync("/AccessRequests?status=Approved")));
    }
}
