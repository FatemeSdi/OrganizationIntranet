extern alias PortalEntry;
extern alias AdminEntry;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrganizationIntranet.Api.Data;
using OrganizationIntranet.Api.Integrations;
using OrganizationIntranet.Api.Security;
using OrganizationIntranet.Application.Abstractions;
using OrganizationIntranet.Contracts;
using OrganizationIntranet.Domain.Entities;
using Xunit;

namespace OrganizationIntranet.SmokeTests;

public sealed class RecordingRequestSmsSender : IAccessRequestSmsSender
{
    public List<(string Mobile, string Message, string Key)> Sent { get; } = new();
    public bool Fail { get; set; }
    public Task SendAsync(string mobile, string message, string key, CancellationToken cancellationToken)
    {
        if (Fail) throw new HttpRequestException("test failure");
        Sent.Add((mobile, message, key)); return Task.CompletedTask;
    }
}
public sealed class NotificationTestHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(respond(request));
}

public sealed partial class PortalSmokeTests
{
    [Fact]
    public async Task Administrative_workflow_rechecks_current_admin_role_and_validates_channel_configuration()
    {
        await Login();
        (await client.PostAsJsonAsync("/api/admin/applications", new ApplicationRequest("پیامک", "SMSAPP", null, null, 1, AccessRequestNotificationChannels: "Both"))).EnsureSuccessStatusCode();
        var created = (await client.GetFromJsonAsync<List<ApplicationDto>>("/api/admin/applications"))!.Single(a => a.ApplicationCode == "SMSAPP");
        Assert.Equal("Both", created.AccessRequestNotificationChannels);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/admin/applications", new ApplicationRequest("نامعتبر", "INVALID", null, null, 1, AccessRequestNotificationChannels: "invalid"))).StatusCode);
        using var scope = api.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Roles.SingleAsync(r => r.RoleCode == "ADMIN")).IsActive = false; await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admin/access-requests")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admin/supervision/sms")).StatusCode);
    }

    [Fact]
    public async Task Organization_catalog_import_is_admin_only_atomic_and_repeatable_with_exact_assignments()
    {
        long fatemehId; int expertId;
        using (var scope = api.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = new User { Username = "fatemeh.sadeghi", Name = "فاطمه", LastName = "صادقی", IsActive = true };
            var role = new Role { RoleCode = "EXPERT", RoleName = "کارشناس نرم‌افزار", IsActive = true };
            db.Users.Add(user); db.Roles.Add(role); await db.SaveChangesAsync(); fatemehId = user.UserId; expertId = role.RoleId;
        }
        await Login("member"); Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/api/admin/catalog-import", null)).StatusCode);
        var token = await Login();
        var preview = await client.GetFromJsonAsync<CatalogImportPreviewDto>("/api/admin/catalog-import"); Assert.Equal(12, preview!.Items.Count);
        using var admin = Ui<AdminEntry::Program>("Admin"); using var ui = admin.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        ui.DefaultRequestHeaders.Add("Cookie", Cookie(admin.Services, token, true));
        var fields = new Dictionary<string,string> { ["__RequestVerificationToken"] = await Antiforgery(ui, "/Admin/Applications/Import") };
        Assert.Equal(HttpStatusCode.Redirect, (await ui.PostAsync("/Admin/Applications/Import", new FormUrlEncodedContent(fields))).StatusCode);
        var second = await (await client.PostAsync("/api/admin/catalog-import", null)).Content.ReadFromJsonAsync<CatalogImportResultDto>(); Assert.Equal(0, second!.Created);
        using var verify = api.Services.CreateScope(); var context = verify.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(13, await context.Applications.CountAsync());
        Assert.Equal("GATEWAY", (await context.Applications.SingleAsync(a => a.IsPublic)).ApplicationCode);
        var direct = await context.ApplicationAccessGrants.Include(g => g.Application).SingleAsync(g => g.UserId == fatemehId); Assert.Equal("PAYSLIP", direct.Application.ApplicationCode);
        var roleGrant = await context.ApplicationAccessGrants.Include(g => g.Application).SingleAsync(g => g.RoleId == expertId); Assert.Equal("ATTENDANCE", roleGrant.Application.ApplicationCode);
        Assert.Equal(2, await context.ApplicationAccessGrants.CountAsync()); Assert.Empty(await context.Notifications.ToListAsync());
    }

    [Fact]
    public async Task Pull_transport_rejects_redirects_and_oversized_responses_without_following_links()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
            ["ApplicationIntegrations:TEST:PullUrl"] = "https://source.example/feed", ["ApplicationIntegrations:TEST:PullKey"] = ApiFactory.IntegrationKey }).Build();
        var calls = 0;
        using var redirectHttp = new HttpClient(new NotificationTestHandler(_ => { calls++; return new(HttpStatusCode.Redirect) { Headers = { Location = new Uri("https://other.example/") } }; }));
        var transport = new NotificationPullClient(redirectHttp, configuration);
        await Assert.ThrowsAsync<HttpRequestException>(() => transport.FetchAsync("TEST", "https://source.example/feed", null, default)); Assert.Equal(1, calls);
        await Assert.ThrowsAsync<InvalidOperationException>(() => transport.FetchAsync("TEST", "http://source.example/feed", null, default)); Assert.Equal(1, calls);
        using var hugeHttp = new HttpClient(new NotificationTestHandler(_ => new(HttpStatusCode.OK) { Content = new StringContent(new string('x', 262145)) }));
        var huge = new NotificationPullClient(hugeHttp, configuration);
        await Assert.ThrowsAsync<InvalidOperationException>(() => huge.FetchAsync("TEST", "https://source.example/feed", null, default));
    }

    [Fact]
    public async Task Database_pending_request_uniqueness_and_integration_batch_rollback_are_enforced()
    {
        using var scope = api.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.ApplicationAccessRequests.Add(new() { ApplicationId = 1, UserId = 2, Reason = "اول" }); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        db.ApplicationAccessRequests.Add(new() { ApplicationId = 1, UserId = 2, Reason = "تکراری" });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync()); db.ChangeTracker.Clear();
        await ConfigureNotificationSource();
        using var source = api.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false }); source.DefaultRequestHeaders.Add("X-Application-Key", ApiFactory.IntegrationKey);
        var first = NotificationBatch().Items[0];
        var batch = new ApplicationNotificationBatch([first, first with { ExternalId = "bad", Username = "does-not-exist" }]);
        Assert.Equal(HttpStatusCode.BadRequest, (await source.PostAsJsonAsync("/api/integrations/applications/TEST/notifications", batch)).StatusCode);
        Assert.Empty(await db.Notifications.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task Access_request_approval_is_scoped_atomic_and_rejects_duplicate_and_stale_decisions()
    {
        await Login("member");
        Assert.Single((await client.GetFromJsonAsync<List<RequestableApplicationDto>>("/api/portal/access-requests/applications"))!);
        (await client.PostAsJsonAsync("/api/portal/access-requests", new CreateAccessRequest(1, "نیاز کاری"))).EnsureSuccessStatusCode();
        var request = Assert.Single((await client.GetFromJsonAsync<AccessRequestListDto>("/api/portal/access-requests"))!.Items);
        Assert.Equal(2, request.UserId); Assert.Equal("Pending", request.Status);
        Assert.Empty((await client.GetFromJsonAsync<List<RequestableApplicationDto>>("/api/portal/access-requests/applications"))!);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/portal/access-requests", new CreateAccessRequest(1, "تکراری"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync($"/api/admin/access-requests/{request.Id}/review", new ReviewAccessRequest(true, null, request.Revision))).StatusCode);
        await Login();
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync($"/api/portal/access-requests/{request.Id}/cancel", new CancelAccessRequest(request.Revision))).StatusCode);
        var alerts = (await client.GetFromJsonAsync<AccessRequestAlertsDto>("/api/admin/supervision/alerts"))!; Assert.Equal(1, alerts.UnreadCount);
        var decision = new ReviewAccessRequest(true, "تأیید شد", request.Revision);
        client.DefaultRequestHeaders.Remove("X-Intranet-Admin");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync($"/api/admin/access-requests/{request.Id}/review", decision)).StatusCode);
        client.DefaultRequestHeaders.Add("X-Intranet-Admin", ApiFactory.AdminClientKey);
        (await client.PostAsJsonAsync($"/api/admin/access-requests/{request.Id}/review", decision)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"/api/admin/access-requests/{request.Id}/review", decision with { Approve = false })).StatusCode);
        using var scope = api.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await db.ApplicationAccessGrants.CountAsync(g => g.ApplicationId == 1 && g.UserId == 2));
        Assert.Equal("Approved", (await db.ApplicationAccessRequests.SingleAsync()).Status);
        await Login("member"); Assert.Single((await client.GetFromJsonAsync<List<PortalApplicationDto>>("/api/portal/applications"))!);
    }

    [Fact]
    public async Task Access_requests_support_cancellation_rejection_and_resubmission_without_grants()
    {
        await Login("member");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/portal/access-requests", new CreateAccessRequest(1, " "))).StatusCode);
        (await client.PostAsJsonAsync("/api/portal/access-requests", new CreateAccessRequest(1, "کاری"))).EnsureSuccessStatusCode();
        var r = Assert.Single((await client.GetFromJsonAsync<AccessRequestListDto>("/api/portal/access-requests"))!.Items);
        (await client.PostAsJsonAsync($"/api/portal/access-requests/{r.Id}/cancel", new CancelAccessRequest(r.Revision))).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"/api/portal/access-requests/{r.Id}/cancel", new CancelAccessRequest(r.Revision))).StatusCode);
        (await client.PostAsJsonAsync("/api/portal/access-requests", new CreateAccessRequest(1, "درخواست مجدد"))).EnsureSuccessStatusCode();
        r = Assert.Single((await client.GetFromJsonAsync<AccessRequestListDto>("/api/portal/access-requests?status=Pending"))!.Items);
        await Login();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/api/admin/access-requests/{r.Id}/review", new ReviewAccessRequest(false, null, r.Revision))).StatusCode);
        (await client.PostAsJsonAsync($"/api/admin/access-requests/{r.Id}/review", new ReviewAccessRequest(false, "نیاز به تأیید واحد", r.Revision))).EnsureSuccessStatusCode();
        using var scope = api.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Empty(await db.ApplicationAccessGrants.ToListAsync());
        Assert.Equal(2, await db.ApplicationAccessRequests.CountAsync());
    }

    [Fact]
    public async Task Supervision_groups_notify_active_users_and_roles_once_and_revocation_hides_inbox()
    {
        long supervisorId; int roleId;
        using (var scope = api.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var role = new Role { RoleName = "نظارت", RoleCode = "SUPERVISOR" };
            var user = new User { Username = "supervisor", Name = "ناظر", LastName = "آزمایش", IsActive = true, MobileNumber = "09121111111" };
            user.PasswordHash = new PasswordService().Hash(user, "test-password"); user.UserRoles.Add(new() { Role = role }); db.Users.Add(user);
            (await db.Applications.SingleAsync()).AccessRequestNotificationChannels = "Both";
            await db.SaveChangesAsync(); supervisorId = user.UserId; roleId = role.RoleId;
        }
        await Login();
        (await client.PostAsJsonAsync("/api/admin/supervision/applications/1/groups", new SupervisionGroupRequest(0, "ناظرین", true, [supervisorId], [roleId], Guid.Empty))).EnsureSuccessStatusCode();
        var group = Assert.Single((await client.GetFromJsonAsync<List<SupervisionGroupDto>>("/api/admin/supervision/applications/1/groups"))!);
        await Login("member");
        (await client.PostAsJsonAsync("/api/portal/access-requests", new CreateAccessRequest(1, "دلیل خصوصی"))).EnsureSuccessStatusCode();
        Assert.Empty((await client.GetFromJsonAsync<AccessRequestListDto>("/api/portal/supervision/inbox"))!.Items);
        await Login("supervisor");
        Assert.Single((await client.GetFromJsonAsync<AccessRequestListDto>("/api/portal/supervision/inbox"))!.Items);
        Assert.Empty((await client.GetFromJsonAsync<List<PortalApplicationDto>>("/api/portal/applications"))!);
        var alerts = (await client.GetFromJsonAsync<AccessRequestAlertsDto>("/api/portal/supervision/alerts"))!;
        Assert.Equal(1, alerts.UnreadCount);
        (await client.PostAsync($"/api/portal/supervision/alerts/{alerts.Items[0].Id}/read", null)).EnsureSuccessStatusCode();
        Assert.Equal(0, (await client.GetFromJsonAsync<AccessRequestAlertsDto>("/api/portal/supervision/alerts"))!.UnreadCount);
        using (var scope = api.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal(2, await db.AccessRequestAlerts.CountAsync());
            Assert.Equal(2, await db.AccessRequestAlerts.CountAsync(a => a.SmsStatus == "Pending"));
        }
        var smsWorker = new AccessRequestSmsWorker(api.Services.GetRequiredService<IServiceScopeFactory>(), api.Services.GetRequiredService<ILogger<AccessRequestSmsWorker>>());
        await smsWorker.DispatchAsync(default); await smsWorker.DispatchAsync(default);
        Assert.Equal(2, api.SmsSender.Sent.Count); Assert.Equal(2, api.SmsSender.Sent.Select(s => s.Key).Distinct().Count());
        Assert.All(api.SmsSender.Sent, s => Assert.DoesNotContain("دلیل خصوصی", s.Message));
        await Login();
        (await client.PostAsJsonAsync("/api/admin/supervision/applications/1/groups", new SupervisionGroupRequest(group.Id, group.Name, false, [], [], group.Revision))).EnsureSuccessStatusCode();
        await Login("supervisor");
        Assert.Empty((await client.GetFromJsonAsync<AccessRequestAlertsDto>("/api/portal/supervision/alerts"))!.Items);
        Assert.Empty((await client.GetFromJsonAsync<AccessRequestListDto>("/api/portal/supervision/inbox"))!.Items);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync($"/api/portal/supervision/alerts/{alerts.Items[0].Id}/read", null)).StatusCode);
    }

    [Theory]
    [InlineData("InApp", true, "NotRequested")]
    [InlineData("Sms", false, "Pending")]
    [InlineData("Both", true, "Pending")]
    public async Task Request_notification_channels_are_snapshotted_per_application(string channels, bool inApp, string smsStatus)
    {
        using var scope = api.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Applications.SingleAsync()).AccessRequestNotificationChannels = channels; await db.SaveChangesAsync();
        await Login("member");
        (await client.PostAsJsonAsync("/api/portal/access-requests", new CreateAccessRequest(1, "کاری"))).EnsureSuccessStatusCode();
        var alert = await db.AccessRequestAlerts.SingleAsync(); Assert.Equal(inApp, alert.InApp); Assert.Equal(smsStatus, alert.SmsStatus);
        await Login(); Assert.Equal(inApp ? 1 : 0, (await client.GetFromJsonAsync<AccessRequestAlertsDto>("/api/admin/supervision/alerts"))!.UnreadCount);
    }

    [Fact]
    public async Task Sms_outbox_records_failure_retries_and_cancels_after_resolution()
    {
        using var scope = api.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Applications.SingleAsync()).AccessRequestNotificationChannels = "Sms"; await db.SaveChangesAsync();
        await Login("member"); (await client.PostAsJsonAsync("/api/portal/access-requests", new CreateAccessRequest(1, "کاری"))).EnsureSuccessStatusCode();
        api.SmsSender.Fail = true;
        var worker = new AccessRequestSmsWorker(api.Services.GetRequiredService<IServiceScopeFactory>(), api.Services.GetRequiredService<ILogger<AccessRequestSmsWorker>>());
        await worker.DispatchAsync(default);
        var alert = await db.AccessRequestAlerts.SingleAsync(); Assert.Equal("Pending", alert.SmsStatus); Assert.Equal(1, alert.SmsAttempts); Assert.NotNull(alert.SmsError);
        var request = await db.ApplicationAccessRequests.SingleAsync();
        (await client.PostAsJsonAsync($"/api/portal/access-requests/{request.ApplicationAccessRequestId}/cancel", new CancelAccessRequest(request.Revision))).EnsureSuccessStatusCode();
        alert.NextAttemptAt = DateTime.UtcNow.AddSeconds(-1); await db.SaveChangesAsync();
        await worker.DispatchAsync(default); await db.Entry(alert).ReloadAsync(); Assert.Equal("Cancelled", alert.SmsStatus); Assert.Empty(api.SmsSender.Sent);
    }

    private async Task ConfigureNotificationSource()
    {
        using var scope = api.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var app = await db.Applications.SingleAsync(); app.BaseUrl = "https://source.example/"; app.IsPublic = true;
        app.Services.Add(new() { ServiceCode = "NOTIFICATIONS", DisplayName = "اعلان", EndpointUrl = "https://source.example/api/notifications" });
        await db.SaveChangesAsync();
    }
    private static ApplicationNotificationBatch NotificationBatch(string externalId = "event-1")
    {
        var time = DateTimeOffset.UtcNow.AddMinutes(-1);
        return new([new(externalId, "member", "نامه جدید", "متن", "https://source.example/letters/42", time, time)]);
    }
    [Fact]
    public async Task External_notifications_require_source_credentials_deduplicate_and_open_only_for_recipient()
    {
        await ConfigureNotificationSource();
        using var source = api.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var batch = NotificationBatch();
        Assert.Equal(HttpStatusCode.Unauthorized, (await source.PostAsJsonAsync("/api/integrations/applications/TEST/notifications", batch)).StatusCode);
        source.DefaultRequestHeaders.Add("X-Application-Key", ApiFactory.IntegrationKey);
        (await source.PostAsJsonAsync("/api/integrations/applications/TEST/notifications", batch)).EnsureSuccessStatusCode();
        Assert.Equal(1, (await (await source.PostAsJsonAsync("/api/integrations/applications/TEST/notifications", batch)).Content.ReadFromJsonAsync<NotificationImportResult>())!.Ignored);
        Assert.Equal(HttpStatusCode.Unauthorized, (await source.GetAsync("/api/admin/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await source.PostAsJsonAsync("/api/integrations/applications/OTHER/notifications", batch)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await source.PostAsJsonAsync("/api/integrations/applications/TEST/notifications", batch with { Items = [batch.Items[0] with { ExternalId = "bad", TargetUrl = "https://evil.example/" }] })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await source.PostAsJsonAsync("/api/integrations/applications/TEST/notifications", batch with { Items = [batch.Items[0] with { ExternalId = "bad", Username = "missing" }] })).StatusCode);
        await Login("member"); var notification = Assert.Single((await client.GetFromJsonAsync<List<PortalNotificationDto>>("/api/portal/notifications"))!);
        Assert.Equal("https://source.example/letters/42", notification.TargetUrl);
        var destination = await (await client.PostAsync($"/api/portal/notifications/{notification.Id}/open", null)).Content.ReadFromJsonAsync<NotificationDestinationDto>();
        Assert.Equal(notification.TargetUrl, destination!.Url);
        Assert.False(Assert.Single((await client.GetFromJsonAsync<List<PortalNotificationDto>>("/api/portal/notifications"))!).Unread);
        await Login(); Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync($"/api/portal/notifications/{notification.Id}/open", null)).StatusCode);
        batch = batch with { Items = [batch.Items[0] with { UpdatedAt = DateTimeOffset.UtcNow, Title = "عنوان جدید" }] };
        (await source.PostAsJsonAsync("/api/integrations/applications/TEST/notifications", batch)).EnsureSuccessStatusCode();
        await Login("member"); Assert.False(Assert.Single((await client.GetFromJsonAsync<List<PortalNotificationDto>>("/api/portal/notifications"))!).Unread);
        using var scope = api.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); Assert.Equal(1, await db.Notifications.CountAsync());
    }

    [Fact]
    public async Task Pull_uses_registered_pinned_endpoint_and_commits_cursor_only_after_valid_import()
    {
        await ConfigureNotificationSource();
        var seen = new List<Uri>(); var batch = NotificationBatch() with { NextCursor = "page-2" };
        api.PullResponse = request => { seen.Add(request.RequestUri!); Assert.True(request.Headers.Contains("X-Intranet-Integration-Key")); return new(HttpStatusCode.OK) { Content = JsonContent.Create(batch) }; };
        var worker = new NotificationPullWorker(api.Services.GetRequiredService<IServiceScopeFactory>(), api.Services.GetRequiredService<IConfiguration>(), api.Services.GetRequiredService<ILogger<NotificationPullWorker>>());
        await worker.PollAsync(default);
        using var scope = api.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var registration = await db.ApplicationServices.SingleAsync(); Assert.Equal("page-2", registration.SyncCursor); Assert.NotNull(registration.LastSyncedAt);
        batch = NotificationBatch("bad") with { Items = [NotificationBatch().Items[0] with { Username = "missing" }], NextCursor = "page-3" };
        await worker.PollAsync(default); await db.Entry(registration).ReloadAsync();
        Assert.Equal("page-2", registration.SyncCursor); Assert.Contains("cursor=page-2", seen[1].Query); Assert.NotNull(registration.LastSyncError);
        registration.EndpointUrl = "https://unapproved.example/api"; await db.SaveChangesAsync(); await worker.PollAsync(default);
        Assert.Equal(2, seen.Count); Assert.Equal(1, await db.Notifications.CountAsync());
    }

    [Fact]
    public async Task Portal_request_card_admin_review_and_notification_redirect_work_with_antiforgery()
    {
        var memberToken = await Login("member");
        using var portal = Ui<PortalEntry::Program>("Portal"); using var ui = portal.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        ui.DefaultRequestHeaders.Add("Cookie", Cookie(portal.Services, memberToken, false));
        var html = await ui.GetStringAsync("/Portal"); Assert.Contains("request-access-card", html); Assert.Contains("id=\"access-requests\"", html);
        var fields = new Dictionary<string,string> { ["RequestedApplicationId"] = "1", ["RequestReason"] = "<script>request</script>" };
        Assert.Equal(HttpStatusCode.BadRequest, (await ui.PostAsync("/Portal?handler=RequestAccess", new FormUrlEncodedContent(fields))).StatusCode);
        fields["__RequestVerificationToken"] = await Antiforgery(ui, "/Portal");
        var submitted = await ui.PostAsync("/Portal?handler=RequestAccess", new FormUrlEncodedContent(fields));
        Assert.Equal(HttpStatusCode.Redirect, submitted.StatusCode); Assert.EndsWith("#access-requests", submitted.Headers.Location!.ToString());
        html = await ui.GetStringAsync("/Portal"); Assert.Contains("&lt;script&gt;request&lt;/script&gt;", html);
        var adminToken = await Login(); using var admin = Ui<AdminEntry::Program>("Admin"); using var adminUi = admin.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        adminUi.DefaultRequestHeaders.Add("Cookie", Cookie(admin.Services, adminToken, true));
        (await adminUi.GetAsync("/Admin?welcome=true")).EnsureSuccessStatusCode();
        foreach (var path in new[] { "/Admin/AccessRequests", "/Admin/AccessRequests/Notifications", "/Admin/Applications/Supervision?id=1" }) (await adminUi.GetAsync(path)).EnsureSuccessStatusCode();
        var r = Assert.Single((await client.GetFromJsonAsync<AccessRequestListDto>("/api/admin/access-requests"))!.Items);
        var review = new Dictionary<string,string> { ["requestId"] = r.Id.ToString(), ["revision"] = r.Revision.ToString(), ["decision"] = "approve", ["__RequestVerificationToken"] = await Antiforgery(adminUi, "/Admin/AccessRequests") };
        Assert.Equal(HttpStatusCode.Redirect, (await adminUi.PostAsync("/Admin/AccessRequests?handler=Review", new FormUrlEncodedContent(review))).StatusCode);
        await ConfigureNotificationSource();
        using (var scope = api.Services.CreateScope()) await scope.ServiceProvider.GetRequiredService<OrganizationIntranet.Application.Services.NotificationIntegrationService>().ImportAsync("TEST", NotificationBatch());
        html = await ui.GetStringAsync("/Portal"); Assert.Contains("notificationId", html); Assert.Contains("OpenNotification", html);
        await Login("member"); var notification = Assert.Single((await client.GetFromJsonAsync<List<PortalNotificationDto>>("/api/portal/notifications"))!);
        var open = new Dictionary<string,string> { ["notificationId"] = notification.Id.ToString(), ["__RequestVerificationToken"] = await Antiforgery(ui, "/Portal") };
        var response = await ui.PostAsync("/Portal?handler=OpenNotification", new FormUrlEncodedContent(open));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode); Assert.Equal("https://source.example/letters/42", response.Headers.Location!.AbsoluteUri);
    }
}
