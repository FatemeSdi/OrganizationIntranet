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
using P = OrganizationIntranet.Contracts.AdminPermissions;

namespace OrganizationIntranet.Tests;

// مدیریت نقش‌ها: شمار کاربران/مجوزها، ویرایش نام و کد، جزئیات و حذف فقط برای نقش استفاده‌نشده.
public sealed partial class PortalSmokeTests
{
    private async Task<List<RoleDto>> Roles() => (await client.GetFromJsonAsync<List<RoleDto>>("/api/admin/roles"))!;
    private async Task<string> Error(HttpResponseMessage response) => (await response.Content.ReadFromJsonAsync<OperationResult>())!.Message!;

    [Fact]
    public async Task Role_list_shows_how_many_users_and_permissions_each_role_has()
    {
        await Login();
        var roles = await Roles();
        var admin = roles.Single(r => r.RoleCode == "ADMIN");
        Assert.Equal((1, P.All.Count), (admin.UserCount, admin.PermissionCount));
        Assert.False(admin.CanDelete);
        var member = roles.Single(r => r.RoleCode == "MEMBER");
        Assert.Equal((1, 0, 0), (member.UserCount, member.PermissionCount, member.LinkCount));
        Assert.False(member.CanDelete); // کاربر دارد
    }

    [Fact]
    public async Task Role_codes_are_validated_unique_and_system_codes_cannot_change()
    {
        await Login();
        (await client.PostAsJsonAsync("/api/admin/roles", new RoleRequest("  کارشناس منابع انسانی ", " hr_expert "))).EnsureSuccessStatusCode();
        var hr = (await Roles()).Single(r => r.RoleCode == "HR_EXPERT");
        Assert.Equal("کارشناس منابع انسانی", hr.RoleName);

        foreach (var bad in new[] { "1HR", "HR-EXPERT", "منابع", "HR EXPERT" })
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/admin/roles", new RoleRequest("x", bad))).StatusCode);
        var duplicate = await client.PostAsJsonAsync("/api/admin/roles", new RoleRequest("تکراری", "member"));
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        Assert.Contains("MEMBER", await Error(duplicate));

        // ویرایش نام و کد
        (await client.PostAsJsonAsync($"/api/admin/roles/{hr.RoleId}", new RoleRequest("کارشناس ارشد منابع انسانی", "HR_SENIOR"))).EnsureSuccessStatusCode();
        Assert.Contains(await Roles(), r => r.RoleId == hr.RoleId && r.RoleCode == "HR_SENIOR" && r.RoleName == "کارشناس ارشد منابع انسانی");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/api/admin/roles/{hr.RoleId}", new RoleRequest("x", "MEMBER"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/admin/roles/999", new RoleRequest("x", "NOPE"))).StatusCode);

        // نقش سیستمی: نام عوض می‌شود، کد نه
        var admin = (await Roles()).Single(r => r.RoleCode == "ADMIN");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/api/admin/roles/{admin.RoleId}", new RoleRequest("مدیر", "SUPER_ADMIN"))).StatusCode);
        (await client.PostAsJsonAsync($"/api/admin/roles/{admin.RoleId}", new RoleRequest("مدیر سامانه", "admin"))).EnsureSuccessStatusCode();
        Assert.Contains(await Roles(), r => r.RoleId == admin.RoleId && r.RoleCode == "ADMIN" && r.RoleName == "مدیر سامانه");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/admin/dashboard")).StatusCode); // مدیر هنوز به پنل دسترسی دارد
    }

    [Fact]
    public async Task Only_unused_non_system_roles_can_be_deleted()
    {
        await Login();
        (await client.PostAsJsonAsync("/api/admin/roles", new RoleRequest("موقت", "TEMP"))).EnsureSuccessStatusCode();
        (await client.PostAsJsonAsync("/api/admin/roles", new RoleRequest("ناظر", "WATCHER"))).EnsureSuccessStatusCode();
        var roles = await Roles();
        int Id(string code) => roles.Single(r => r.RoleCode == code).RoleId;

        var admin = await client.PostAsync($"/api/admin/roles/{Id("ADMIN")}/delete", null);
        Assert.Equal(HttpStatusCode.BadRequest, admin.StatusCode);
        var member = await client.PostAsync($"/api/admin/roles/{Id("MEMBER")}/delete", null);
        Assert.Equal(HttpStatusCode.BadRequest, member.StatusCode);
        Assert.Contains("1 کاربر", await Error(member));

        // نقشی که فقط در دسترسی مستقیم یک سامانه آمده هم حذف نمی‌شود
        using (var scope = api.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.ApplicationAccessGrants.Add(new ApplicationAccess { ApplicationId = db.Applications.First().ApplicationId, RoleId = Id("WATCHER"), CreatedAt = DateTime.UtcNow });
            db.SaveChanges();
        }
        Assert.Equal(1, (await Roles()).Single(r => r.RoleCode == "WATCHER").LinkCount);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync($"/api/admin/roles/{Id("WATCHER")}/delete", null)).StatusCode);

        (await client.PostAsync($"/api/admin/roles/{Id("TEMP")}/delete", null)).EnsureSuccessStatusCode();
        Assert.DoesNotContain(await Roles(), r => r.RoleCode == "TEMP");
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync($"/api/admin/roles/{Id("TEMP")}/delete", null)).StatusCode);
    }

    [Fact]
    public async Task Role_details_list_members_only_for_holders_of_user_view_and_edits_need_role_edit()
    {
        await Login();
        var adminRole = (await Roles()).Single(r => r.RoleCode == "ADMIN");
        var details = (await client.GetFromJsonAsync<RoleDetailsDto>($"/api/admin/roles/{adminRole.RoleId}"))!;
        Assert.Contains(details.Users, u => u.Username == "admin");
        Assert.Equal(P.All.Count, details.Permissions.Count);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/admin/roles/999")).StatusCode);

        GrantMember("ROLE_READER", P.IntranetAdmin, P.RoleView);
        await Login("member");
        details = (await client.GetFromJsonAsync<RoleDetailsDto>($"/api/admin/roles/{adminRole.RoleId}"))!;
        Assert.True(details.UsersHidden);
        Assert.Empty(details.Users);                       // بدون USER_VIEW نام اعضا افشا نمی‌شود
        Assert.Equal(1, details.Role.UserCount);           // ولی شمار آن دیده می‌شود
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync($"/api/admin/roles/{adminRole.RoleId}", new RoleRequest("x", "ADMIN"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync($"/api/admin/roles/{adminRole.RoleId}/delete", null)).StatusCode);
    }

    [Fact]
    public async Task Admin_role_pages_show_counts_edit_and_delete_roles()
    {
        var token = await Login();
        (await client.PostAsJsonAsync("/api/admin/roles", new RoleRequest("موقت", "TEMP"))).EnsureSuccessStatusCode();
        var temp = (await Roles()).Single(r => r.RoleCode == "TEMP");
        var adminRole = (await Roles()).Single(r => r.RoleCode == "ADMIN");
        long adminId;
        using (var scope = api.Services.CreateScope()) adminId = scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.Single(u => u.Username == "admin").UserId;
        using var admin = Ui<AdminEntry::Program>("Admin");
        using var browser = admin.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        browser.DefaultRequestHeaders.Add("Cookie", CookieWith(admin.Services, token, adminId, P.All.Select(p => p.Code), DateTimeOffset.UtcNow));

        var list = WebUtility.HtmlDecode(await browser.GetStringAsync("/Admin/Roles"));
        Assert.Contains("<th>کاربران</th>", list);
        Assert.Contains($"href=\"/Admin/Roles/Edit?id={temp.RoleId}\"", list);

        var adminPage = WebUtility.HtmlDecode(await browser.GetStringAsync($"/Admin/Roles/Edit?id={adminRole.RoleId}"));
        Assert.Contains("نقش سیستمی حذف نمی‌شود", adminPage);
        Assert.Contains("href=\"/Admin/Users/Edit?id=", adminPage);   // اعضا با پیوند به کاربر
        Assert.DoesNotContain("handler=Delete", adminPage);            // دکمه‌ی حذف برای نقش سیستمی نیست
        Assert.Equal(HttpStatusCode.NotFound, (await browser.GetAsync("/Admin/Roles/Edit?id=999")).StatusCode);

        var editUrl = $"/Admin/Roles/Edit?id={temp.RoleId}";
        var save = await browser.PostAsync(editUrl, new FormUrlEncodedContent(new Dictionary<string, string>
            { ["__RequestVerificationToken"] = await Antiforgery(browser, editUrl), ["RoleName"] = "موقت <b>ویرایش‌شده</b>", ["RoleCode"] = "TEMP_2" }));
        Assert.Equal(HttpStatusCode.Redirect, save.StatusCode);
        Assert.Contains(await Roles(), r => r.RoleId == temp.RoleId && r.RoleCode == "TEMP_2" && r.RoleName == "موقت <b>ویرایش‌شده</b>");
        Assert.DoesNotContain("<b>ویرایش‌شده</b>", await browser.GetStringAsync("/Admin/Roles")); // نام نقش encode می‌شود

        var invalid = await browser.PostAsync(editUrl, new FormUrlEncodedContent(new Dictionary<string, string>
            { ["__RequestVerificationToken"] = await Antiforgery(browser, editUrl), ["RoleName"] = "x", ["RoleCode"] = "MEMBER" }));
        Assert.Equal(HttpStatusCode.OK, invalid.StatusCode); // خطا روی همان صفحه (توست)
        Assert.Contains("MEMBER", WebUtility.HtmlDecode(await invalid.Content.ReadAsStringAsync()));

        var delete = await browser.PostAsync(editUrl + "&handler=Delete", new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = await Antiforgery(browser, editUrl) }));
        Assert.Equal(HttpStatusCode.Redirect, delete.StatusCode);
        Assert.DoesNotContain(await Roles(), r => r.RoleId == temp.RoleId);
    }

    [Fact]
    public async Task Role_viewer_sees_details_read_only_and_cannot_post_edits()
    {
        GrantMember("ROLE_READER", P.IntranetAdmin, P.RoleView);
        var token = await Login("member");
        int memberRole;
        using (var scope = api.Services.CreateScope()) memberRole = scope.ServiceProvider.GetRequiredService<AppDbContext>().Roles.Single(r => r.RoleCode == "MEMBER").RoleId;
        using var admin = Ui<AdminEntry::Program>("Admin");
        using var browser = admin.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        browser.DefaultRequestHeaders.Add("Cookie", CookieWith(admin.Services, token, MemberId(), [P.IntranetAdmin, P.RoleView], DateTimeOffset.UtcNow));

        var url = $"/Admin/Roles/Edit?id={memberRole}";
        var page = WebUtility.HtmlDecode(await browser.GetStringAsync(url));
        Assert.Contains("جزئیات نقش", page);
        Assert.DoesNotContain("type=\"submit\"", page);
        Assert.Contains("مجوز «مشاهده‌ی کاربران» لازم است", page);
        var post = await browser.PostAsync(url, new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = await Antiforgery(browser, url), ["RoleName"] = "x", ["RoleCode"] = "X" }));
        Assert.Contains("/Account/AccessDenied", post.Headers.Location?.OriginalString ?? ""); // PostRequiresPermission → Forbid
        using (var scope = api.Services.CreateScope())
            Assert.Equal("MEMBER", scope.ServiceProvider.GetRequiredService<AppDbContext>().Roles.AsNoTracking().Single(r => r.RoleId == memberRole).RoleCode);
    }
}
