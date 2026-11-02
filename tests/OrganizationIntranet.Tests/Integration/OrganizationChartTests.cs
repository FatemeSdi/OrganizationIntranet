extern alias AdminEntry;
using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using OrganizationIntranet.Api.Data;
using OrganizationIntranet.Contracts;
using Xunit;
using P = OrganizationIntranet.Contracts.AdminPermissions;

namespace OrganizationIntranet.Tests;

// چارت سازمانی (migration 012): درخت واحدها، قواعد ساخت و جابه‌جایی و حذف، مجوزهای ORG_VIEW/ORG_EDIT و پیوند کاربر به واحد.
public sealed partial class PortalSmokeTests
{
    private async Task<int> AddUnit(string title, int? parentId = null, int order = 0, bool active = true)
    {
        var created = await client.PostAsJsonAsync("/api/admin/org/units", new OrgUnitRequest(title, parentId, order, active));
        created.EnsureSuccessStatusCode();
        return (await Units()).Single(u => u.Title == title && u.ParentId == parentId).UnitId;
    }
    private async Task<List<OrgUnitDto>> Units() => (await client.GetFromJsonAsync<List<OrgUnitDto>>("/api/admin/org/units"))!;
    private Task<HttpResponseMessage> UpdateUnit(int id, string title, int? parentId, int order = 0, bool active = true) =>
        client.PostAsJsonAsync($"/api/admin/org/units/{id}", new OrgUnitRequest(title, parentId, order, active));

    [Fact]
    public async Task Org_tree_lists_parents_before_children_in_display_order_with_counts_and_full_paths()
    {
        await Login();
        var root = await AddUnit("اداره کل");
        var tech = await AddUnit("معاونت فنی", root, order: 2);
        var admin = await AddUnit("معاونت اداری", root, order: 1);
        var it = await AddUnit("فناوری اطلاعات", tech);

        var tree = await Units();
        Assert.Equal(["اداره کل", "معاونت اداری", "معاونت فنی", "فناوری اطلاعات"], tree.Select(u => u.Title));      // ترتیب نمایش، پدر پیش از فرزند
        Assert.Equal([0, 1, 1, 2], tree.Select(u => u.Depth));
        Assert.Equal("اداره کل/معاونت فنی/فناوری اطلاعات", tree.Single(u => u.UnitId == it).FullPath);
        Assert.Equal(2, tree.Single(u => u.UnitId == root).ChildCount);
        Assert.Equal(0, tree.Single(u => u.UnitId == admin).ChildCount);
    }

    [Fact]
    public async Task Org_unit_rules_reject_duplicates_slashes_cycles_unknown_parents_and_too_deep_trees()
    {
        await Login();
        var root = await AddUnit("ریشه");
        var child = await AddUnit("فرزند", root);

        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/admin/org/units", new OrgUnitRequest("  ", null, 0))).StatusCode);          // عنوان خالی
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/admin/org/units", new OrgUnitRequest("الف/ب", null, 0))).StatusCode);       // جداکننده‌ی مسیر
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/admin/org/units", new OrgUnitRequest(new string('ا', 201), null, 0))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/admin/org/units", new OrgUnitRequest("فرزند", root, 0))).StatusCode);      // تکراری زیر همان پدر
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/admin/org/units", new OrgUnitRequest("فرزند", null, 0))).StatusCode);              // همان عنوان زیر پدر دیگر مجاز است
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/admin/org/units", new OrgUnitRequest("یتیم", 99999, 0))).StatusCode);      // پدر ناموجود

        Assert.Equal(HttpStatusCode.BadRequest, (await UpdateUnit(root, "ریشه", child)).StatusCode);                  // زیر زیرمجموعه‌ی خودش
        Assert.Equal(HttpStatusCode.BadRequest, (await UpdateUnit(root, "ریشه", root)).StatusCode);                   // زیر خودش
        Assert.Equal(HttpStatusCode.NotFound, (await UpdateUnit(99999, "x", null)).StatusCode);

        var parent = root;
        for (var level = 2; level <= Application.Services.OrganizationService.MaxDepth; level++) parent = await AddUnit("سطح " + level, parent);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/admin/org/units", new OrgUnitRequest("خیلی عمیق", parent, 0))).StatusCode);
    }

    [Fact]
    public async Task Renaming_or_moving_a_unit_rebuilds_the_paths_of_its_whole_subtree()
    {
        await Login();
        var a = await AddUnit("الف");
        var b = await AddUnit("ب");
        var c = await AddUnit("ج", a);
        var d = await AddUnit("د", c);

        Assert.Equal(HttpStatusCode.OK, (await UpdateUnit(a, "الف‌نو", null)).StatusCode);                                // تغییر نام
        Assert.Equal("الف‌نو/ج/د", (await Units()).Single(u => u.UnitId == d).FullPath);
        Assert.Equal(HttpStatusCode.OK, (await UpdateUnit(c, "ج", b)).StatusCode);                                       // جابه‌جایی با زیرمجموعه‌اش
        var tree = await Units();
        Assert.Equal("ب/ج", tree.Single(u => u.UnitId == c).FullPath);
        Assert.Equal("ب/ج/د", tree.Single(u => u.UnitId == d).FullPath);
        Assert.Equal(0, tree.Single(u => u.UnitId == a).ChildCount);
        Assert.Equal(HttpStatusCode.OK, (await UpdateUnit(c, "ج", null)).StatusCode);                                    // تبدیل به ریشه
        Assert.Equal("ج/د", (await Units()).Single(u => u.UnitId == d).FullPath);
    }

    [Fact]
    public async Task Deactivating_a_unit_deactivates_its_subtree_and_reactivating_needs_an_active_parent()
    {
        await Login();
        var root = await AddUnit("ریشه");
        var mid = await AddUnit("میانی", root);
        var leaf = await AddUnit("برگ", mid);

        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/admin/org/units/{root}/toggle", null)).StatusCode);
        Assert.All(await Units(), u => Assert.False(u.IsActive));
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync($"/api/admin/org/units/{leaf}/toggle", null)).StatusCode);   // پدر هنوز غیرفعال
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/admin/org/units", new OrgUnitRequest("تازه", root, 0))).StatusCode); // واحد فعال زیر غیرفعال
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/admin/org/units/{root}/toggle", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/admin/org/units/{mid}/toggle", null)).StatusCode);
        Assert.Equal([true, true, false], (await Units()).Select(u => u.IsActive));                                            // ریشه و میانی فعال، برگ هنوز غیرفعال
    }

    [Fact]
    public async Task A_user_can_be_placed_in_an_active_unit_and_that_unit_is_then_protected_from_deletion()
    {
        await Login();
        var root = await AddUnit("ریشه");
        var child = await AddUnit("فرزند", root);
        var inactive = await AddUnit("کنار", null, active: false);
        var memberId = UserId("member");
        var user = (await client.GetFromJsonAsync<UserDto>($"/api/admin/users/{memberId}"))!;
        Assert.Null(user.UnitId);

        UserEditRequest Edit(int? unit) => new() { Id = memberId, Name = user.Name, LastName = user.LastName, Username = user.Username, IsActive = true, SelectedRoleIds = user.SelectedRoleIds, UnitId = unit };
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/admin/users", Edit(99999))).StatusCode);   // واحد ناموجود
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/admin/users", Edit(inactive))).StatusCode); // واحد غیرفعال
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/admin/users", Edit(child))).StatusCode);
        Assert.Equal(child, (await client.GetFromJsonAsync<UserDto>($"/api/admin/users/{memberId}"))!.UnitId);
        Assert.Equal(1, (await Units()).Single(u => u.UnitId == child).UserCount);

        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync($"/api/admin/org/units/{child}/delete", null)).StatusCode);  // کاربر دارد
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync($"/api/admin/org/units/{root}/delete", null)).StatusCode);   // زیرمجموعه دارد
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/admin/users", Edit(null))).StatusCode);                   // برداشتن واحد
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/admin/org/units/{child}/delete", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/admin/org/units/{root}/delete", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync($"/api/admin/org/units/{root}/delete", null)).StatusCode);
    }

    [Fact]
    public async Task A_user_keeps_a_unit_that_was_deactivated_later_without_being_forced_to_change_it()
    {
        await Login();
        var unit = await AddUnit("واحد");
        var memberId = UserId("member");
        var user = (await client.GetFromJsonAsync<UserDto>($"/api/admin/users/{memberId}"))!;
        UserEditRequest Edit() => new() { Id = memberId, Name = user.Name, LastName = user.LastName, Username = user.Username, IsActive = true, SelectedRoleIds = user.SelectedRoleIds, UnitId = unit };
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/admin/users", Edit())).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/admin/org/units/{unit}/toggle", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/admin/users", Edit())).StatusCode);                // ذخیره‌ی بدون تغییر واحد
        Assert.Equal(unit, (await client.GetFromJsonAsync<UserDto>($"/api/admin/users/{memberId}"))!.UnitId);
    }

    [Fact]
    public async Task Org_endpoints_need_ORG_VIEW_or_ORG_EDIT_on_top_of_panel_access()
    {
        await Login();
        var unit = await AddUnit("واحد");

        GrantMember("ORG_READER", P.IntranetAdmin, P.OrgView);
        await Login("member");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/admin/org/units")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/admin/org/units", new OrgUnitRequest("نو", null, 0))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await UpdateUnit(unit, "نو", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync($"/api/admin/org/units/{unit}/toggle", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync($"/api/admin/org/units/{unit}/delete", null)).StatusCode);

        GrantMember("ORG_EDITOR", P.OrgEdit);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/admin/org/units", new OrgUnitRequest("نو", null, 0))).StatusCode);   // مجوز زنده، بدون ورود دوباره
    }

    [Fact]
    public async Task Org_permissions_are_system_permissions_held_by_the_admin_role()
    {
        Assert.Contains(P.All, p => p.Code == P.OrgView);
        Assert.Contains(P.All, p => p.Code == P.OrgEdit);
        Assert.True(P.IsSystem(P.OrgEdit));
        await Login();
        var permissions = (await client.GetFromJsonAsync<List<PermissionDto>>("/api/admin/permissions"))!;
        Assert.All(new[] { P.OrgView, P.OrgEdit }, code => Assert.True(permissions.Single(p => p.PermissionCode == code).RoleCount >= 1));
    }

    [Fact]
    public async Task Org_admin_pages_render_and_create_a_unit_from_the_form()
    {
        var (admin, browser, _, _, _) = await AdminBrowserAsync();
        using (admin) using (browser)
        {
            var empty = WebUtility.HtmlDecode(await browser.GetStringAsync("/Admin/Organization"));
            Assert.Contains("چارت سازمانی خالی است", empty);
            Assert.Contains("چارت سازمانی", WebUtility.HtmlDecode(await browser.GetStringAsync("/Admin")));        // پیوند منو

            var token = await Antiforgery(browser, "/Admin/Organization");
            var created = await browser.PostAsync("/Admin/Organization?handler=Create", new FormUrlEncodedContent(new Dictionary<string, string>
            { ["Title"] = "اداره کل", ["DisplayOrder"] = "0", ["__RequestVerificationToken"] = token }));
            Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);

            var list = WebUtility.HtmlDecode(await browser.GetStringAsync("/Admin/Organization"));
            Assert.Contains("اداره کل", list);
            var id = (await Units()).Single().UnitId;
            var edit = WebUtility.HtmlDecode(await browser.GetStringAsync($"/Admin/Organization/Edit?id={id}"));
            Assert.Contains("حذف واحد", edit);
            Assert.Equal(HttpStatusCode.NotFound, (await browser.GetAsync("/Admin/Organization/Edit?id=99999")).StatusCode);

            var duplicate = await browser.PostAsync("/Admin/Organization?handler=Create", new FormUrlEncodedContent(new Dictionary<string, string>
            { ["Title"] = "اداره کل", ["DisplayOrder"] = "0", ["__RequestVerificationToken"] = token }));
            Assert.Equal(HttpStatusCode.OK, duplicate.StatusCode);                                                     // خطا با پیام، نه ریدایرکت
            Assert.Contains("واحدی با همین عنوان", WebUtility.HtmlDecode(await duplicate.Content.ReadAsStringAsync()));

            var userForm = WebUtility.HtmlDecode(await browser.GetStringAsync($"/Admin/Users/Edit?id={UserId("member")}"));
            Assert.Contains("واحد سازمانی", userForm);
            Assert.Contains("اداره کل", userForm);
        }
    }
}
