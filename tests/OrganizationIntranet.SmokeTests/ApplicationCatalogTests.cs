extern alias PortalEntry;
extern alias AdminEntry;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrganizationIntranet.Api.Data;
using OrganizationIntranet.Contracts;
using OrganizationIntranet.Domain.Entities;
using Xunit;
using AppEntity = OrganizationIntranet.Domain.Entities.Application;

namespace OrganizationIntranet.SmokeTests;

public sealed partial class PortalSmokeTests
{
    [Fact]
    public async Task Administrative_API_requires_Admin_server_credential_even_with_admin_token_and_forged_origin()
    {
        var token = await Login();
        using var external = api.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        external.DefaultRequestHeaders.Add("X-Intranet-Client", ApiFactory.ClientKey);
        external.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        external.DefaultRequestHeaders.Add("Origin", "https://localhost:7231");
        external.DefaultRequestHeaders.Referrer = new Uri("https://localhost:7231/Admin?welcome=true");
        foreach (var path in new[] { "/api/admin/applications", "/API/ADMIN/applications/1", "/api/admin/users", "/api/admin/roles", "/api/admin/permissions", "/api/admin/publications", "/api/admin/authentication" })
            Assert.Equal(HttpStatusCode.Forbidden, (await external.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await external.PostAsync("/api/admin/applications/1/toggle", null)).StatusCode);
        external.DefaultRequestHeaders.Add("X-Intranet-Admin", ApiFactory.ClientKey);
        Assert.Equal(HttpStatusCode.Forbidden, (await external.GetAsync("/api/admin/applications")).StatusCode);
        external.DefaultRequestHeaders.Remove("X-Intranet-Admin");
        external.DefaultRequestHeaders.Add("X-Intranet-Admin", ApiFactory.AdminClientKey);
        (await external.GetAsync("/api/admin/applications")).EnsureSuccessStatusCode();
        external.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", await Login("member"));
        Assert.Equal(HttpStatusCode.Forbidden, (await external.GetAsync("/api/admin/applications")).StatusCode);
    }

    [Fact]
    public async Task Admin_UI_enforces_configured_origin_and_Portal_cannot_proxy_administrative_requests()
    {
        var token = await Login();
        using var admin = Ui<AdminEntry::Program>("Admin");
        using var ui = admin.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        ui.DefaultRequestHeaders.Add("Cookie", Cookie(admin.Services, token, true));
        var csrf = await Antiforgery(ui, "/Admin/Applications/Edit?id=1");
        ui.DefaultRequestHeaders.Add("Origin", "https://localhost:7230");
        var fields = new Dictionary<string,string> { ["Id"] = "1", ["Input.ApplicationName"] = "Blocked", ["Input.ApplicationCode"] = "TEST", ["Input.Icon"] = "apps", ["__RequestVerificationToken"] = csrf };
        Assert.Equal(HttpStatusCode.Forbidden, (await ui.PostAsync("/Admin/Applications/Edit?id=1", new FormUrlEncodedContent(fields))).StatusCode);
        ui.DefaultRequestHeaders.Remove("Origin");
        ui.DefaultRequestHeaders.Host = "localhost:7230";
        Assert.Equal(HttpStatusCode.BadRequest, (await ui.GetAsync("/Admin?welcome=true")).StatusCode);
        ui.DefaultRequestHeaders.Host = null;
        var html = await ui.GetStringAsync("/Admin/Applications/Edit?id=1");
        Assert.DoesNotContain(ApiFactory.AdminClientKey, html);
        Assert.DoesNotContain(ApiFactory.ClientKey, html);
        using var portal = Ui<PortalEntry::Program>("Portal");
        using var scope = portal.Services.CreateScope();
        var accessor = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Http.IHttpContextAccessor>();
        accessor.HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        accessor.HttpContext.Request.Path = "/Admin";
        accessor.HttpContext.User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(
            [new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, "ADMIN")], "test"));
        try
        {
            var proxy = scope.ServiceProvider.GetRequiredService<PortalEntry::OrganizationIntranet.UI.ApiClient>();
            var error = await Assert.ThrowsAsync<PortalEntry::OrganizationIntranet.UI.ApiException>(() => proxy.GetAsync<object>("api/admin/applications"));
            Assert.Equal(HttpStatusCode.Forbidden, error.Status);
        }
        finally { accessor.HttpContext = null; }
    }

    [Fact]
    public async Task Catalog_unions_public_user_and_active_roles_without_duplicates_and_reflects_revocation()
    {
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var member = await db.Users.SingleAsync(u => u.Username == "member");
        var role = await db.Roles.SingleAsync(r => r.RoleCode == "MEMBER");
        var disabledRole = new Role { RoleName = "غیرفعال", RoleCode = "DISABLED", IsActive = false };
        db.UserRoles.Add(new UserRole { UserId = member.UserId, Role = disabledRole });
        var direct = new AppEntity { ApplicationName = "مستقیم", ApplicationCode = "DIRECT", DisplayOrder = 2,
            AccessGrants = [new() { UserId = member.UserId }, new() { RoleId = role.RoleId }] };
        var byRole = new AppEntity { ApplicationName = "نقش", ApplicationCode = "ROLE", DisplayOrder = 3,
            AccessGrants = [new() { RoleId = role.RoleId }] };
        var publicApp = new AppEntity { ApplicationName = "عمومی", ApplicationCode = "PUBLIC", IsPublic = true, DisplayOrder = 1 };
        db.Applications.AddRange(direct, byRole, publicApp,
            new() { ApplicationName = "خاموش", ApplicationCode = "OFF", IsPublic = true, IsActive = false },
            new() { ApplicationName = "نقش خاموش", ApplicationCode = "OFFROLE", AccessGrants = [new() { Role = disabledRole }] });
        await db.SaveChangesAsync();
        await Login("member");
        var apps = (await client.GetFromJsonAsync<List<PortalApplicationDto>>("/api/portal/applications?userId=1"))!;
        Assert.Equal(new[] { publicApp.ApplicationId, direct.ApplicationId, byRole.ApplicationId }, apps.Select(a => a.ApplicationId));
        db.UserRoles.Remove(await db.UserRoles.SingleAsync(ur => ur.UserId == member.UserId && ur.RoleId == role.RoleId));
        await db.SaveChangesAsync();
        apps = (await client.GetFromJsonAsync<List<PortalApplicationDto>>("/api/portal/applications"))!;
        Assert.Equal(new[] { publicApp.ApplicationId, direct.ApplicationId }, apps.Select(a => a.ApplicationId));
        db.ApplicationAccessGrants.Remove(await db.ApplicationAccessGrants.SingleAsync(g => g.ApplicationId == direct.ApplicationId && g.UserId == member.UserId));
        await db.SaveChangesAsync();
        Assert.Single((await client.GetFromJsonAsync<List<PortalApplicationDto>>("/api/portal/applications"))!);
        await Login();
        Assert.Single((await client.GetFromJsonAsync<List<PortalApplicationDto>>("/api/portal/applications"))!); // No implicit ADMIN grant.
    }

    [Fact]
    public async Task Catalog_notifications_are_scoped_to_user_access_and_registered_active_service()
    {
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var member = await db.Users.SingleAsync(u => u.Username == "member");
        var admin = await db.Users.SingleAsync(u => u.Username == "admin");
        var app = await db.Applications.SingleAsync(); app.IsPublic = true;
        app.Services.Add(new() { ServiceCode = "NOTIFICATIONS", DisplayName = "اعلان" });
        var hidden = new AppEntity { ApplicationName = "پنهان", ApplicationCode = "HIDDEN", Services = [new() { ServiceCode = "NOTIFICATIONS", DisplayName = "اعلان" }] };
        var disabled = new AppEntity { ApplicationName = "بدون سرویس فعال", ApplicationCode = "NOSERVICE", IsPublic = true,
            Services = [new() { ServiceCode = "NOTIFICATIONS", DisplayName = "اعلان", IsActive = false }] };
        var own = new UserNotification { UserId = member.UserId, Notification = new() { Application = app, Title = "اعلان من", Message = "متن" } };
        var other = new UserNotification { UserId = admin.UserId, Notification = new() { Application = app, Title = "اعلان دیگری", Message = "متن" } };
        var inaccessible = new UserNotification { UserId = member.UserId, Notification = new() { Application = hidden, Title = "محرمانه", Message = "متن" } };
        var inactive = new UserNotification { UserId = member.UserId, Notification = new() { Application = disabled, Title = "خاموش", Message = "متن" } };
        db.UserNotifications.AddRange(own, other, inaccessible, inactive); await db.SaveChangesAsync();
        await Login("member");
        var cards = (await client.GetFromJsonAsync<List<PortalApplicationDto>>("/api/portal/applications"))!;
        Assert.Equal(1, cards.Single(a => a.ApplicationId == app.ApplicationId).UnreadCount);
        Assert.Equal(0, cards.Single(a => a.ApplicationId == disabled.ApplicationId).UnreadCount);
        var notifications = (await client.GetFromJsonAsync<List<PortalNotificationDto>>("/api/portal/notifications"))!;
        Assert.Equal(own.UserNotificationId, Assert.Single(notifications).Id);
        (await client.PostAsync("/api/portal/notifications/read", null)).EnsureSuccessStatusCode();
        await db.Entry(own).ReloadAsync(); await db.Entry(other).ReloadAsync(); await db.Entry(inaccessible).ReloadAsync(); await db.Entry(inactive).ReloadAsync();
        Assert.True(own.IsRead); Assert.NotNull(own.ReadAt);
        Assert.False(other.IsRead); Assert.False(inaccessible.IsRead); Assert.False(inactive.IsRead);
    }

    [Fact]
    public async Task Catalog_admin_configuration_validates_and_round_trips_atomically()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/portal/applications")).StatusCode);
        await Login("member");
        var request = new ApplicationConfigurationRequest(new("فرزین", "FARZIN", "https://example.org/login", "گردش نامه", 4, "mail", false, true, false),
            [2, 2], [2, 2], [new("notifications", "اعلان", null), new("tasks", "وظایف", "https://example.org/tasks")]);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/admin/applications/1", request)).StatusCode);
        await Login();
        (await client.PostAsJsonAsync("/api/admin/applications/1", request)).EnsureSuccessStatusCode();
        var data = (await client.GetFromJsonAsync<ApplicationConfigurationDto>("/api/admin/applications/1"))!;
        Assert.Single(data.UserIds); Assert.Single(data.RoleIds); Assert.Equal(2, data.Services.Count);
        Assert.False(data.Application.RequiresLogin); Assert.True(data.Application.IsInternetAccessible);
        Assert.Equal("mail", data.Application.Icon);
        foreach (var invalid in new[] {
            request with { Application = request.Application with { BaseUrl = "javascript:alert(1)" } },
            request with { Application = request.Application with { Icon = "<svg onload=alert(1)>" } },
            request with { UserIds = [99999] },
            request with { Services = [new("NOTIFICATIONS", "اول", null), new("notifications", "تکراری", null)] },
            request with { Services = [new("BAD", "بد", "https://user:password@example.org")] }
        }) Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/admin/applications/1", invalid)).StatusCode);
        data = (await client.GetFromJsonAsync<ApplicationConfigurationDto>("/api/admin/applications/1"))!;
        Assert.Equal("https://example.org/login", data.Application.BaseUrl); Assert.Equal(2, data.Services.Count);
        (await client.PostAsJsonAsync("/api/admin/applications/1", request with { UserIds = [], RoleIds = [], Services = [] })).EnsureSuccessStatusCode();
        data = (await client.GetFromJsonAsync<ApplicationConfigurationDto>("/api/admin/applications/1"))!;
        Assert.Empty(data.UserIds); Assert.Empty(data.RoleIds); Assert.Empty(data.Services);
    }

    [Fact]
    public async Task Catalog_database_rejects_ambiguous_principals_and_duplicate_grants()
    {
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        foreach (var grant in new[] { new ApplicationAccess { ApplicationId = 1 }, new ApplicationAccess { ApplicationId = 1, UserId = 1, RoleId = 1 } })
        {
            db.ApplicationAccessGrants.Add(grant);
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync()); db.ChangeTracker.Clear();
        }
        db.ApplicationAccessGrants.Add(new() { ApplicationId = 1, UserId = 1 }); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        db.ApplicationAccessGrants.Add(new() { ApplicationId = 1, UserId = 1 });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Catalog_UI_renders_database_cards_safely_and_saves_admin_form()
    {
        var token = await Login();
        (await client.PostAsJsonAsync("/api/admin/applications/1", new ApplicationConfigurationRequest(
            new("<script>alert(1)</script>", "TEST", "https://example.org", "<b>summary</b>", 1, "mail", false, true, true), [], [], []))).EnsureSuccessStatusCode();
        using var portal = Ui<PortalEntry::Program>("Portal");
        using var admin = Ui<AdminEntry::Program>("Admin");
        var options = new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false };
        using var ui = portal.CreateClient(options); using var adminUi = admin.CreateClient(options);
        ui.DefaultRequestHeaders.Add("Cookie", Cookie(portal.Services, token, true));
        adminUi.DefaultRequestHeaders.Add("Cookie", Cookie(admin.Services, token, true));
        var html = await ui.GetStringAsync("/Portal");
        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", html);
        Assert.DoesNotContain("<script>alert(1)</script>", html);
        Assert.Contains("href=\"https://example.org\"", html); Assert.Contains("application-icons.svg#mail", html);
        Assert.DoesNotContain("const allSystems", html);
        var csrf = await Antiforgery(adminUi, "/Admin/Applications/Edit?id=1"); Assert.NotEmpty(csrf);
        var fields = new Dictionary<string,string> { ["Id"] = "1", ["Input.ApplicationName"] = "ویرایش", ["Input.ApplicationCode"] = "TEST", ["Input.Icon"] = "clock", ["Input.IsPublic"] = "true" };
        Assert.Equal(HttpStatusCode.BadRequest, (await adminUi.PostAsync("/Admin/Applications/Edit?id=1", new FormUrlEncodedContent(fields))).StatusCode);
        fields["__RequestVerificationToken"] = csrf;
        Assert.Equal(HttpStatusCode.Redirect, (await adminUi.PostAsync("/Admin/Applications/Edit?id=1", new FormUrlEncodedContent(fields))).StatusCode);
        var updated = (await client.GetFromJsonAsync<ApplicationConfigurationDto>("/api/admin/applications/1"))!;
        Assert.Equal("ویرایش", updated.Application.ApplicationName); Assert.Equal("clock", updated.Application.Icon);
        Assert.Equal(HttpStatusCode.BadRequest, (await ui.PostAsync("/Portal?handler=ReadNotifications", null)).StatusCode);
    }
}
