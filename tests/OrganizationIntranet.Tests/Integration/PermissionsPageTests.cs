extern alias AdminEntry;
using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using OrganizationIntranet.Api.Data;
using OrganizationIntranet.Application.Abstractions;
using OrganizationIntranet.Contracts;
using Xunit;

namespace OrganizationIntranet.Tests;

// صفحه‌ی مجوزها: فهرست فقط‌خواندنی بدون ردیابی EF با شمار درست نقش‌ها، و متن راهنما با رفتار واقعی (اعمال زنده) هم‌خوان است.
public sealed partial class PortalSmokeTests
{
    [Fact]
    public async Task Permission_list_is_read_without_tracking_and_counts_roles()
    {
        await Login();
        var list = (await client.GetFromJsonAsync<List<PermissionDto>>("/api/admin/permissions"))!;
        Assert.All(AdminPermissions.All, item => Assert.Contains(list, p => p.PermissionCode == item.Code));
        Assert.All(list.Where(p => AdminPermissions.IsSystem(p.PermissionCode)), p => { Assert.True(p.RoleCount >= 1); Assert.Equal(p.RoleCount, p.SelectedRoleIds.Count); });

        using var scope = api.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IIntranetRepository>();
        Assert.NotEmpty(await repository.GetPermissionsAsync());
        Assert.Empty(scope.ServiceProvider.GetRequiredService<AppDbContext>().ChangeTracker.Entries());   // AsNoTracking: هیچ موجودیتی ردیابی نمی‌شود
    }

    [Fact]
    public async Task Assign_page_explains_that_changes_apply_without_logging_in_again()
    {
        var (admin, browser, _, _, permissionId) = await AdminBrowserAsync();
        using (admin) using (browser)
        {
            var html = WebUtility.HtmlDecode(await browser.GetStringAsync($"/Admin/Permissions/Assign?id={permissionId}"));
            Assert.Contains("فوراً اعمال می‌شود", html);
            Assert.Contains("نیازی به خروج و ورود دوباره نیست", html);
            Assert.DoesNotContain("پس از ورود دوباره‌ی کاربران اعمال می‌شود", html);                        // متن قدیمی دیگر غلط نیست
        }
    }
}
