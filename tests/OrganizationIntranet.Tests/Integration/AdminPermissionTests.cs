extern alias AdminEntry;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrganizationIntranet.Api.Data;
using OrganizationIntranet.Contracts;
using OrganizationIntranet.Domain.Entities;
using Xunit;
using P = OrganizationIntranet.Contracts.AdminPermissions;

namespace OrganizationIntranet.Tests;

// مجوزدهی پنل مدیریت بر اساس Permission (migration 008): هر صفحه/endpoint مجوز دقیق خودش را می‌خواهد، API زنده از بانک بررسی می‌کند
// و مدیر از پنل قفل نمی‌شود.
public sealed partial class PortalSmokeTests
{
    /// <summary>نقش تازه با مجوزهای داده‌شده برای کاربر member (مثلاً «ویراستار محتوا»).</summary>
    private int GrantMember(string roleCode, params string[] codes)
    {
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var role = new Role { RoleCode = roleCode, RoleName = roleCode, IsActive = true };
        foreach (var code in codes) role.RolePermissions.Add(new RolePermission { Permission = db.Permissions.Single(p => p.PermissionCode == code) });
        db.Users.Include(u => u.UserRoles).Single(u => u.Username == "member").UserRoles.Add(new UserRole { Role = role });
        db.SaveChanges();
        return role.RoleId;
    }

    private static string CookieWith(IServiceProvider services, string token, long userId, IEnumerable<string> permissions, DateTimeOffset synced)
    {
        var options = services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(CookieAuthenticationDefaults.AuthenticationScheme);
        var claims = new List<Claim> { new("api_token", token), new(ClaimTypes.NameIdentifier, userId.ToString()), new(ClaimTypes.GivenName, "نام"), new(ClaimTypes.Surname, "آزمایشی"),
            new("claims_synced", synced.ToUnixTimeSeconds().ToString()) };
        claims.AddRange(permissions.Select(p => new Claim("permission", p)));
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)),
            new AuthenticationProperties { IssuedUtc = DateTimeOffset.UtcNow, ExpiresUtc = DateTimeOffset.UtcNow.AddDays(14), AllowRefresh = true }, CookieAuthenticationDefaults.AuthenticationScheme);
        return options.Cookie.Name + "=" + options.TicketDataFormat.Protect(ticket);
    }

    private long MemberId()
    {
        using var scope = api.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.Single(u => u.Username == "member").UserId;
    }

    [Fact]
    public async Task Admin_API_is_authorized_by_exact_permissions_checked_live_from_the_database()
    {
        var roleId = GrantMember("CONTENT_EDITOR", P.IntranetAdmin, P.PublicationEdit);
        await Login("member");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/admin/publications")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admin/users")).StatusCode);          // بدون USER_VIEW
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/admin/roles", new RoleRequest("x", "X"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admin/authentication")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admin/access-requests")).StatusCode);
        var dashboard = (await client.GetFromJsonAsync<DashboardDto>("/api/admin/dashboard"))!;
        Assert.Empty(dashboard.RecentUsers);                                                                    // آمار کلی بله، فهرست کاربران نه
        Assert.Equal(2, dashboard.TotalUsers);

        // گرفتن مجوز بلافاصله اثر دارد؛ همان توکن دیگر کار نمی‌کند (نه پس از ورود بعدی)
        using (var scope = api.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.RolePermissions.RemoveRange(db.RolePermissions.Where(rp => rp.RoleId == roleId && rp.Permission.PermissionCode == P.PublicationEdit));
            db.SaveChanges();
        }
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admin/publications")).StatusCode);

        // بدون INTRANET_ADMIN حتی با مجوز دقیق هم پنل بسته است
        GrantMember("EDITOR_WITHOUT_PANEL", P.PublicationEdit);
        using (var scope = api.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Roles.Single(r => r.RoleId == roleId).IsActive = false;
            db.SaveChanges();
        }
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admin/publications")).StatusCode);
    }

    [Fact]
    public async Task Admin_cannot_be_locked_out_of_the_panel()
    {
        await Login();
        var roles = (await client.GetFromJsonAsync<List<RoleDto>>("/api/admin/roles"))!;
        var admin = roles.Single(r => r.RoleCode == "ADMIN");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync($"/api/admin/roles/{admin.RoleId}/toggle", null)).StatusCode);
        var permissions = (await client.GetFromJsonAsync<List<PermissionDto>>("/api/admin/permissions"))!;
        Assert.Equal(P.All.Select(p => p.Code).Order(), permissions.Where(p => P.IsSystem(p.PermissionCode)).Select(p => p.PermissionCode).Order());
        var panel = permissions.Single(p => p.PermissionCode == P.IntranetAdmin);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync($"/api/admin/permissions/{panel.PermissionId}/toggle", null)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/api/admin/permissions/{panel.PermissionId}/roles", new RolesRequest([]))).StatusCode);

        var users = (await client.GetFromJsonAsync<List<UserDto>>("/api/admin/users"))!;
        var me = users.Single(u => u.Username == "admin");
        var demote = new UserEditRequest { Id = me.UserId, Name = me.Name, LastName = me.LastName, Username = me.Username, IsActive = true, SelectedRoleIds = [] };
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/admin/users", demote)).StatusCode); // آخرین مدیر فعال
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/admin/users", new UserEditRequest { Id = me.UserId, Name = me.Name, LastName = me.LastName, Username = me.Username, IsActive = false, SelectedRoleIds = [admin.RoleId] })).StatusCode); // غیرفعال‌کردن آخرین مدیر

        var member = users.Single(u => u.Username == "member");
        (await client.PostAsJsonAsync("/api/admin/users", new UserEditRequest { Id = member.UserId, Name = member.Name, LastName = member.LastName, Username = member.Username, IsActive = true, SelectedRoleIds = [admin.RoleId] })).EnsureSuccessStatusCode();
        (await client.PostAsJsonAsync("/api/admin/users", demote)).EnsureSuccessStatusCode(); // حالا مدیر دیگری هست
    }

    [Fact]
    public async Task Admin_pages_menu_and_edits_follow_the_users_permissions()
    {
        GrantMember("VIEWER", P.IntranetAdmin, P.RoleView, P.PublicationEdit);
        var token = await Login("member");
        using var admin = Ui<AdminEntry::Program>("Admin");
        using var browser = admin.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        browser.DefaultRequestHeaders.Add("Cookie", CookieWith(admin.Services, token, MemberId(), [P.IntranetAdmin, P.RoleView, P.PublicationEdit], DateTimeOffset.UtcNow));

        var dashboard = WebUtility.HtmlDecode(await browser.GetStringAsync("/Admin"));
        Assert.Contains("href=\"/Admin/Publications\"", dashboard);
        Assert.Contains("href=\"/Admin/Roles\"", dashboard);
        Assert.DoesNotContain("href=\"/Admin/Users\"", dashboard);                  // منو فقط بخش‌های مجاز
        Assert.DoesNotContain("href=\"/Admin/Authentication\"", dashboard);
        Assert.DoesNotContain(">تنظیمات<", dashboard);                             // گروه خالی منو هم پنهان
        Assert.DoesNotContain("درخواست‌های دسترسی در انتظار", dashboard);          // کارت بدون مجوز بارگذاری نمی‌شود
        Assert.DoesNotContain("آخرین کاربران ثبت‌شده", dashboard);

        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync("/Admin/Publications")).StatusCode);
        foreach (var denied in new[] { "/Admin/Users", "/Admin/Authentication", "/Admin/Applications", "/Admin/AccessRequests" })
            Assert.NotEqual(HttpStatusCode.OK, (await browser.GetAsync(denied)).StatusCode);

        // مشاهده‌ی نقش‌ها بله، ساخت نقش (ROLE_EDIT) نه: فرم ساخت نمایش داده نمی‌شود و POST هم رد می‌شود
        var roles = WebUtility.HtmlDecode(await browser.GetStringAsync("/Admin/Roles"));
        Assert.DoesNotContain("افزودن نقش", roles);
        Assert.Contains("disabled=\"disabled\"", roles);
        var csrf = await Antiforgery(browser, "/Admin/Roles");
        var create = await browser.PostAsync("/Admin/Roles?handler=Create", new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = csrf, ["RoleName"] = "x", ["RoleCode"] = "X" }));
        Assert.NotEqual(HttpStatusCode.OK, create.StatusCode);
        using (var scope = api.Services.CreateScope())
            Assert.False(scope.ServiceProvider.GetRequiredService<AppDbContext>().Roles.Any(r => r.RoleCode == "X"));
    }

    [Fact]
    public async Task Admin_UI_claims_resync_from_the_API_so_permission_changes_need_no_new_login()
    {
        var token = await Login();
        using var admin = Ui<AdminEntry::Program>("Admin");
        using var browser = admin.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        long adminId;
        using (var scope = api.Services.CreateScope()) adminId = scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.Single(u => u.Username == "admin").UserId;
        // کوکی قدیمی (پیش از migration 008) بدون Claim مجوز و با زمان همگام‌سازی کهنه: باید خودکار از API به‌روز شود
        browser.DefaultRequestHeaders.Add("Cookie", CookieWith(admin.Services, token, adminId, [], DateTimeOffset.UtcNow.AddMinutes(-30)));
        var response = await browser.GetAsync("/Admin/Users");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(response.Headers.GetValues("Set-Cookie"), c => c.Contains("OrganizationIntranet.UI")); // کوکی با Claimهای تازه دوباره صادر شد

        // همان کوکی تازه (همگام‌شده) ولی بدون مجوز: صفحه بسته است
        using var stale = admin.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        stale.DefaultRequestHeaders.Add("Cookie", CookieWith(admin.Services, token, adminId, [], DateTimeOffset.UtcNow));
        Assert.NotEqual(HttpStatusCode.OK, (await stale.GetAsync("/Admin/Users")).StatusCode);
    }

    [Fact]
    public async Task Only_users_with_the_panel_permission_can_sign_in_to_Admin()
    {
        await Login("member");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admin/dashboard")).StatusCode);
        GrantMember("PANEL_ONLY", P.IntranetAdmin);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/admin/dashboard")).StatusCode); // بدون ورود دوباره
    }
}
