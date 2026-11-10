extern alias AdminEntry;
using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrganizationIntranet.Api.Data;
using OrganizationIntranet.Contracts;
using OrganizationIntranet.Domain.Entities;
using Xunit;
using P = OrganizationIntranet.Contracts.AdminPermissions;

namespace OrganizationIntranet.Tests;

// اعلان سازمانی از پنل (migration 015): گیرنده کاربر/واحد/همه، فهرست و پس‌گرفتن/بازگرداندن با حفظ وضعیت خوانده‌شدن.
public sealed partial class PortalSmokeTests
{
    /// <summary>سامانه‌ی فرستنده‌ی اعلان‌های سازمانی (کد INTERANET) و چند کاربر فعال/غیرفعال در واحدها برای آزمون.</summary>
    private void EnsureHomeApplication()
    {
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (!db.Applications.Any(a => a.ApplicationCode == "INTERANET"))
        { db.Applications.Add(new OrganizationIntranet.Domain.Entities.Application { ApplicationName = "اینترانت", ApplicationCode = "INTERANET", IsActive = true }); db.SaveChanges(); }
    }

    private long AddUserInUnit(string username, int? unitId, bool active = true)
    {
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = new User { Username = username, Name = "آزمون", LastName = username, IsActive = active, UnitId = unitId, CreatedAt = DateTime.UtcNow };
        db.Users.Add(user); db.SaveChanges();
        return user.UserId;
    }

    private Task<HttpResponseMessage> SendNotification(string audience, long? userId = null, int? unitId = null, string title = "اطلاعیه", string message = "متن اطلاعیه") =>
        client.PostAsJsonAsync("/api/admin/notifications", new AdminNotificationRequest(title, message, audience, userId, unitId));

    private async Task<List<AdminNotificationDto>> SentNotifications() => (await client.GetFromJsonAsync<AdminNotificationListDto>("/api/admin/notifications"))!.Items;

    private async Task<List<PortalNotificationDto>> MemberNotifications()
    {
        await Login("member");
        return (await client.GetFromJsonAsync<List<PortalNotificationDto>>("/api/portal/notifications"))!;
    }

    [Fact]
    public async Task Notification_to_everyone_reaches_active_users_only_and_shows_in_the_bell_without_any_application_access()
    {
        EnsureHomeApplication();
        AddUserInUnit("gone", null, active: false);
        await Login();
        var sent = await SendNotification(AdminNotificationAudience.All);
        Assert.Equal(HttpStatusCode.OK, sent.StatusCode);
        var item = (await SentNotifications()).Single();
        Assert.Equal(AdminNotificationAudience.All, item.AudienceType);
        Assert.Equal(2, item.RecipientCount);                                                                       // admin و member؛ کاربر غیرفعال نه
        Assert.Contains("برای 2 نفر", (await sent.Content.ReadFromJsonAsync<OperationResult>())!.Message);

        var bell = await MemberNotifications();                                                                     // member به هیچ سامانه‌ای دسترسی ندارد
        var mine = Assert.Single(bell);
        Assert.Equal("اطلاعیه", mine.Title);
        Assert.True(mine.Unread);
        Assert.Equal("اینترانت", mine.System);
        using var scope = api.Services.CreateScope();
        Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<AppDbContext>().UserNotifications.CountAsync(n => n.User.Username == "gone"));
    }

    [Fact]
    public async Task Notification_to_a_unit_includes_its_sub_units_and_excludes_siblings_prefix_lookalikes_and_inactive_users()
    {
        EnsureHomeApplication();
        await Login();
        var tech = await AddUnit("معاونت فنی");
        var it = await AddUnit("فناوری اطلاعات", tech);
        var lookalike = await AddUnit("معاونت فنی ۲");
        var other = await AddUnit("معاونت اداری");
        AddUserInUnit("in.tech", tech); AddUserInUnit("in.it", it); AddUserInUnit("in.lookalike", lookalike); AddUserInUnit("in.other", other); AddUserInUnit("off.tech", tech, active: false);

        Assert.Equal(HttpStatusCode.OK, (await SendNotification(AdminNotificationAudience.Unit, unitId: tech)).StatusCode);
        var item = (await SentNotifications()).Single();
        Assert.Equal(2, item.RecipientCount);
        Assert.Equal("معاونت فنی", item.AudienceLabel);
        using var scope = api.Services.CreateScope();
        var receivers = await scope.ServiceProvider.GetRequiredService<AppDbContext>().UserNotifications.Select(n => n.User.Username).OrderBy(x => x).ToListAsync();
        Assert.Equal(["in.it", "in.tech"], receivers);
    }

    [Fact]
    public async Task Notification_to_one_user_and_validation_of_every_audience()
    {
        EnsureHomeApplication();
        await Login();
        var memberId = UserId("member");
        var inactive = AddUserInUnit("off.user", null, active: false);
        var empty = await AddUnit("خالی");
        var off = await AddUnit("کنار", null, active: false);

        Assert.Equal(HttpStatusCode.OK, (await SendNotification(AdminNotificationAudience.User, userId: memberId)).StatusCode);
        Assert.Equal(1, (await SentNotifications()).Single().RecipientCount);
        Assert.Equal("نام آزمایشی", (await SentNotifications()).Single().AudienceLabel);                      // نام کاربر گیرنده
        Assert.Single(await MemberNotifications());
        await Login();                                                                                   // دوباره مدیر (MemberNotifications با member وارد شد)

        foreach (var bad in new[]
        {
            await SendNotification(AdminNotificationAudience.User, userId: inactive),                    // کاربر غیرفعال
            await SendNotification(AdminNotificationAudience.User, userId: 99999),                       // ناموجود
            await SendNotification(AdminNotificationAudience.User),                                      // بدون کاربر
            await SendNotification(AdminNotificationAudience.Unit, unitId: empty),                       // واحد بدون کاربر
            await SendNotification(AdminNotificationAudience.Unit, unitId: off),                         // واحد غیرفعال
            await SendNotification(AdminNotificationAudience.Unit, unitId: 99999),
            await SendNotification("Everyone"),                                                          // گیرنده‌ی نامعتبر
            await SendNotification(AdminNotificationAudience.All, title: "  "),
            await SendNotification(AdminNotificationAudience.All, title: new string('ع', 201)),
            await SendNotification(AdminNotificationAudience.All, message: ""),
            await SendNotification(AdminNotificationAudience.All, message: new string('م', 2001)),
        })
            Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        Assert.Single(await SentNotifications());                                                        // هیچ‌کدام از ردشده‌ها ساخته نشد
    }

    [Fact]
    public async Task A_withdrawn_notification_disappears_for_recipients_and_returns_with_its_read_state()
    {
        EnsureHomeApplication();
        await Login();
        Assert.Equal(HttpStatusCode.OK, (await SendNotification(AdminNotificationAudience.User, userId: UserId("member"))).StatusCode);
        var id = (await SentNotifications()).Single().NotificationId;

        var bell = await MemberNotifications();
        var delivery = Assert.Single(bell);
        await client.PostAsync("/api/portal/notifications/read", null);                                  // خوانده‌شده
        Assert.False(Assert.Single(await MemberNotifications()).Unread);

        await Login();
        var off = await client.PostAsync($"/api/admin/notifications/{id}/deactivate", null);
        Assert.Equal(HttpStatusCode.OK, off.StatusCode);
        Assert.False((await SentNotifications()).Single().IsActive);
        Assert.Empty(await MemberNotifications());                                                       // محو شد
        var page = (await client.GetFromJsonAsync<NotificationCenterDto>("/api/portal/notifications/page"))!;
        Assert.Equal(0, page.AllCount);

        await Login();
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/admin/notifications/{id}/deactivate", null)).StatusCode);   // تکرار بی‌اثر
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/admin/notifications/{id}/activate", null)).StatusCode);
        var back = Assert.Single(await MemberNotifications());
        Assert.False(back.Unread);                                                                       // وضعیت خوانده‌شدن حفظ شده
        Assert.Equal(1, (await client.GetFromJsonAsync<NotificationCenterDto>("/api/portal/notifications/page"))!.AllCount);
    }

    [Fact]
    public async Task Notifications_from_other_applications_are_not_listed_or_toggled_and_stay_active()
    {
        EnsureHomeApplication();
        long foreign;
        using (var scope = api.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var n = new Notification { Title = "از سامانه", Message = "م", ApplicationId = db.Applications.Single(a => a.ApplicationCode == "TEST").ApplicationId, CreatedAt = DateTime.UtcNow };
            db.Notifications.Add(n); db.SaveChanges(); foreign = n.NotificationId;
            Assert.True(n.IsActive);
            Assert.Null(n.AudienceType);
        }
        await Login();
        Assert.Empty(await SentNotifications());
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync($"/api/admin/notifications/{foreign}/deactivate", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync("/api/admin/notifications/99999/activate", null)).StatusCode);
    }

    [Fact]
    public async Task Sending_needs_the_NOTIFICATION_SEND_permission_checked_live()
    {
        EnsureHomeApplication();
        GrantMember("NOTIFY_READER", P.IntranetAdmin, P.UserView);
        await Login("member");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admin/notifications")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendNotification(AdminNotificationAudience.All)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/api/admin/notifications/1/deactivate", null)).StatusCode);
        GrantMember("NOTIFY_SENDER", P.NotificationSend);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/admin/notifications")).StatusCode);     // بدون ورود دوباره
        Assert.Equal(HttpStatusCode.OK, (await SendNotification(AdminNotificationAudience.All)).StatusCode);
        Assert.Contains(P.All, p => p.Code == P.NotificationSend);
    }

    [Fact]
    public async Task Admin_page_sends_a_notification_lists_it_and_withdraws_it()
    {
        EnsureHomeApplication();
        var (admin, browser, _, _, _) = await AdminBrowserAsync();
        using (admin) using (browser)
        {
            var empty = WebUtility.HtmlDecode(await browser.GetStringAsync("/Admin/Notifications"));
            Assert.Contains("هنوز اعلانی ارسال نشده است", empty);
            var token = await Antiforgery(browser, "/Admin/Notifications");
            var posted = await browser.PostAsync("/Admin/Notifications?handler=Send", new FormUrlEncodedContent(new Dictionary<string, string>
            { ["AudienceType"] = "All", ["Title"] = "قطعی سامانه", ["Message"] = "فردا شب سامانه‌ها قطع است.", ["__RequestVerificationToken"] = token }));
            Assert.Equal(HttpStatusCode.Redirect, posted.StatusCode);
            var list = WebUtility.HtmlDecode(await browser.GetStringAsync("/Admin/Notifications"));
            Assert.Contains("قطعی سامانه", list);
            Assert.Contains("پس‌گرفتن", list);

            var invalid = await browser.PostAsync("/Admin/Notifications?handler=Send", new FormUrlEncodedContent(new Dictionary<string, string>
            { ["AudienceType"] = "Unit", ["Title"] = "x", ["Message"] = "y", ["__RequestVerificationToken"] = token }));
            Assert.Equal(HttpStatusCode.OK, invalid.StatusCode);
            Assert.Contains("واحد انتخاب‌شده وجود ندارد", WebUtility.HtmlDecode(await invalid.Content.ReadAsStringAsync()));

            var id = (await SentNotificationsAsAdmin()).Single().NotificationId;
            var withdrawn = await browser.PostAsync($"/Admin/Notifications?handler=SetActive&id={id}&active=false", new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token }));
            Assert.Equal(HttpStatusCode.Redirect, withdrawn.StatusCode);
            Assert.Contains("بازگرداندن", WebUtility.HtmlDecode(await browser.GetStringAsync("/Admin/Notifications")));
        }
    }

    private async Task<List<AdminNotificationDto>> SentNotificationsAsAdmin()
    {
        await Login();
        return await SentNotifications();
    }
}
