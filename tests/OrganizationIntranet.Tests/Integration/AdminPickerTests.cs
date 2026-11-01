extern alias AdminEntry;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using OrganizationIntranet.Api.Data;
using OrganizationIntranet.Contracts;
using Xunit;

namespace OrganizationIntranet.Tests;

// انتخاب کاربر/نقش با فهرست کشویی قابل جست‌وجو، ثبت لحظه‌ای بدون دکمه‌ی ذخیره، آیکن، نشانی ورود الزامی و ترتیب نمایش اختیاری.
public sealed partial class PortalSmokeTests
{
    private async Task<int> CreateApplicationAsync(string code, int? order = null, string? baseUrl = "https://app.example/")
    {
        var response = await client.PostAsJsonAsync("/api/admin/applications", new ApplicationRequest("سامانه " + code, code, baseUrl, null, order));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await client.GetFromJsonAsync<List<ApplicationDto>>("/api/admin/applications"))!.Single(a => a.ApplicationCode == code).ApplicationId;
    }

    [Fact]
    public async Task Application_base_url_is_required_and_an_empty_order_goes_to_the_last_row()
    {
        await Login();
        var noUrl = await client.PostAsJsonAsync("/api/admin/applications", new ApplicationRequest("بدون آدرس", "NOURL", null, null, 5));
        Assert.Equal(HttpStatusCode.BadRequest, noUrl.StatusCode);
        Assert.Contains("نشانی ورود", (await noUrl.Content.ReadFromJsonAsync<OperationResult>())!.Message);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/admin/applications", new ApplicationRequest("آدرس خالی", "BLANKURL", "   ", null, 5))).StatusCode);

        var first = await CreateApplicationAsync("ORD_A", 10);
        var second = await CreateApplicationAsync("ORD_B");                                              // ترتیب خالی: بعد از بزرگ‌ترین
        var third = await CreateApplicationAsync("ORD_C");
        var apps = (await client.GetFromJsonAsync<List<ApplicationDto>>("/api/admin/applications"))!.ToDictionary(a => a.ApplicationId);
        Assert.Equal(10, apps[first].DisplayOrder);
        Assert.True(apps[second].DisplayOrder > apps[first].DisplayOrder);
        Assert.Equal(apps[second].DisplayOrder + 1, apps[third].DisplayOrder);                           // هر سامانه‌ی تازه آخرین ردیف است

        var existing = (await client.GetFromJsonAsync<ApplicationConfigurationDto>($"/api/admin/applications/{first}"))!;
        var moved = existing.Application;
        var save = await client.PostAsJsonAsync($"/api/admin/applications/{first}", new ApplicationConfigurationRequest(
            new(moved.ApplicationName, moved.ApplicationCode, moved.BaseUrl, moved.Description, null, moved.Icon, moved.RequiresLogin, moved.IsInternetAccessible, moved.IsPublic, moved.AccessRequestNotificationChannels),
            existing.UserIds, existing.RoleIds, []));
        Assert.Equal(HttpStatusCode.OK, save.StatusCode);
        var afterSave = (await client.GetFromJsonAsync<List<ApplicationDto>>("/api/admin/applications"))!.ToDictionary(a => a.ApplicationId);
        Assert.True(afterSave[first].DisplayOrder > afterSave[third].DisplayOrder);                      // ویرایش با ترتیب خالی هم آن را آخر می‌برد
    }

    [Fact]
    public async Task Application_access_can_be_toggled_one_principal_at_a_time()
    {
        await Login();
        var app = await CreateApplicationAsync("TOGGLE");
        var memberId = UserId("member");
        var roleId = (await client.GetFromJsonAsync<List<RoleDto>>("/api/admin/roles"))!.First(r => r.RoleCode == "MEMBER").RoleId;
        async Task<ApplicationConfigurationDto> Config() => (await client.GetFromJsonAsync<ApplicationConfigurationDto>($"/api/admin/applications/{app}"))!;
        Task<HttpResponseMessage> Toggle(long? user, int? role, bool granted) => client.PostAsJsonAsync($"/api/admin/applications/{app}/access", new AccessToggleRequest(user, role, granted));

        Assert.Equal(HttpStatusCode.OK, (await Toggle(memberId, null, true)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Toggle(memberId, null, true)).StatusCode);                // تکرار بی‌اثر است
        Assert.Equal([memberId], (await Config()).UserIds);
        Assert.Equal(HttpStatusCode.OK, (await Toggle(null, roleId, true)).StatusCode);
        var both = await Config();
        Assert.Equal([memberId], both.UserIds);
        Assert.Equal([roleId], both.RoleIds);

        Assert.Equal(HttpStatusCode.OK, (await Toggle(memberId, null, false)).StatusCode);               // برداشتن یکی بقیه را نگه می‌دارد
        var afterRemove = await Config();
        Assert.Empty(afterRemove.UserIds);
        Assert.Equal([roleId], afterRemove.RoleIds);
        Assert.Equal(HttpStatusCode.OK, (await Toggle(memberId, null, false)).StatusCode);               // برداشتن مورد نبوده هم خطا نیست

        Assert.Equal(HttpStatusCode.BadRequest, (await Toggle(null, null, true)).StatusCode);            // هیچ‌کدام
        Assert.Equal(HttpStatusCode.BadRequest, (await Toggle(memberId, roleId, true)).StatusCode);      // هر دو
        Assert.Equal(HttpStatusCode.BadRequest, (await Toggle(999999, null, true)).StatusCode);          // کاربر ناموجود
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/admin/applications/999999/access", new AccessToggleRequest(memberId, null, true))).StatusCode);

        await Login("member");                                                                           // بدون مجوز ویرایش سامانه
        Assert.Equal(HttpStatusCode.Forbidden, (await Toggle(memberId, null, true)).StatusCode);
    }

    [Fact]
    public async Task A_users_role_can_be_toggled_instantly_but_the_last_admin_keeps_the_admin_role()
    {
        await Login();
        var memberId = UserId("member");
        var adminId = UserId("admin");
        var roles = (await client.GetFromJsonAsync<List<RoleDto>>("/api/admin/roles"))!;
        var adminRole = roles.Single(r => r.RoleCode == "ADMIN").RoleId;
        var memberRole = roles.Single(r => r.RoleCode == "MEMBER").RoleId;
        Task<HttpResponseMessage> Toggle(long user, int role, bool granted) => client.PostAsJsonAsync($"/api/admin/users/{user}/role", new UserRoleToggleRequest(role, granted));
        async Task<List<int>> RolesOf(long user) => (await client.GetFromJsonAsync<UserDto>($"/api/admin/users/{user}"))!.SelectedRoleIds;

        Assert.Equal(HttpStatusCode.OK, (await Toggle(memberId, adminRole, true)).StatusCode);
        Assert.Contains(adminRole, await RolesOf(memberId));
        Assert.Contains(memberRole, await RolesOf(memberId));                                            // نقش قبلی می‌ماند
        Assert.Equal(HttpStatusCode.OK, (await Toggle(memberId, adminRole, true)).StatusCode);           // تکرار بی‌اثر
        Assert.Equal(HttpStatusCode.OK, (await Toggle(memberId, adminRole, false)).StatusCode);
        Assert.DoesNotContain(adminRole, await RolesOf(memberId));

        var lastAdmin = await Toggle(adminId, adminRole, false);                                         // admin تنها مدیر فعال است
        Assert.Equal(HttpStatusCode.BadRequest, lastAdmin.StatusCode);
        Assert.Contains(adminRole, await RolesOf(adminId));
        Assert.Equal(HttpStatusCode.BadRequest, (await Toggle(memberId, 999999, true)).StatusCode);      // نقش ناموجود
        Assert.Equal(HttpStatusCode.NotFound, (await Toggle(999999, memberRole, true)).StatusCode);

        await Login("member");
        Assert.Equal(HttpStatusCode.Forbidden, (await Toggle(memberId, memberRole, false)).StatusCode);
    }

    private static async Task<HttpResponseMessage> PostJsonWithToken(HttpClient browser, string url, string tokenPage, object body, bool withToken = true)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json") };
        if (withToken) request.Headers.Add("RequestVerificationToken", await Antiforgery(browser, tokenPage));
        return await browser.SendAsync(request);
    }

    [Fact]
    public async Task Admin_pickers_save_instantly_through_page_handlers_and_report_errors_for_the_toast()
    {
        var (admin, browser, _, memberId, _) = await AdminBrowserAsync();
        using (admin) using (browser)
        {
            var page = "/Admin/Applications/Edit?id=1";
            var html = WebUtility.HtmlDecode(await browser.GetStringAsync(page));
            Assert.Contains("data-instant-url=\"/Admin/Applications/Edit?id=1&handler=Access\"", html);
            Assert.Contains("data-icon-picker", html);                                                   // انتخاب آیکن با نمایش خود آیکن
            Assert.Contains("type=\"url\"", html);
            Assert.Contains("خالی = آخرین ردیف", html);
            Assert.Contains("data-picker-toggle", html);                                                 // فلش فهرست کشویی
            Assert.Contains("placeholder=\"کاربر را انتخاب کنید\"", html);                              // متن «انتخاب کنید» نه «جست‌وجو»
            Assert.Contains("placeholder=\"نقش را انتخاب کنید\"", html);

            var url = "/Admin/Applications/Edit?id=1&handler=Access";
            var added = await PostJsonWithToken(browser, url, page, new { kind = "user", id = memberId, granted = true });
            Assert.Equal(HttpStatusCode.OK, added.StatusCode);
            Assert.True((await added.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("ok").GetBoolean());
            Assert.Contains(memberId, (await client.GetFromJsonAsync<ApplicationConfigurationDto>("/api/admin/applications/1"))!.UserIds);   // همان لحظه در بانک است

            var removed = await PostJsonWithToken(browser, url, page, new { kind = "user", id = memberId, granted = false });
            Assert.Equal(HttpStatusCode.OK, removed.StatusCode);
            Assert.DoesNotContain(memberId, (await client.GetFromJsonAsync<ApplicationConfigurationDto>("/api/admin/applications/1"))!.UserIds);

            var invalid = await PostJsonWithToken(browser, url, page, new { kind = "user", id = 999999, granted = true });
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);                                 // پیام خطا برای toast
            var body = await invalid.Content.ReadFromJsonAsync<JsonElement>();
            Assert.False(body.GetProperty("ok").GetBoolean());
            Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("message").GetString()));

            Assert.NotEqual(HttpStatusCode.OK, (await PostJsonWithToken(browser, url, page, new { kind = "user", id = memberId, granted = true }, withToken: false)).StatusCode); // بدون antiforgery رد می‌شود
        }
    }

    [Fact]
    public async Task User_roles_use_the_searchable_picker_with_instant_save_for_existing_users_only()
    {
        var (admin, browser, _, memberId, _) = await AdminBrowserAsync();
        using (admin) using (browser)
        {
            var existing = WebUtility.HtmlDecode(await browser.GetStringAsync($"/Admin/Users/Edit?id={memberId}"));
            Assert.Contains("data-field=\"SelectedRoleIds\"", existing);
            Assert.Contains($"data-instant-url=\"/Admin/Users/Edit?id={memberId}&handler=Role\"", existing);
            Assert.DoesNotContain("switch-row mb-0", existing);                                           // دیگر فهرست بلند سوییچ‌ها نیست

            var creating = WebUtility.HtmlDecode(await browser.GetStringAsync("/Admin/Users/Edit"));
            Assert.Contains("data-field=\"SelectedRoleIds\"", creating);
            Assert.DoesNotContain("data-instant-url=\"/", creating);                                      // کاربر جدید با دکمه‌ی ذخیره ثبت می‌شود

            var roleId = (await client.GetFromJsonAsync<List<RoleDto>>("/api/admin/roles"))!.First(r => r.RoleCode == "ADMIN").RoleId;
            var page = $"/Admin/Users/Edit?id={memberId}";
            var ok = await PostJsonWithToken(browser, $"{page}&handler=Role", page, new { kind = "role", id = roleId, granted = true });
            Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
            Assert.Contains(roleId, (await client.GetFromJsonAsync<UserDto>($"/api/admin/users/{memberId}"))!.SelectedRoleIds);

            var newUser = await PostJsonWithToken(browser, "/Admin/Users/Edit?handler=Role", "/Admin/Users/Edit", new { kind = "role", id = roleId, granted = true });
            Assert.Equal(HttpStatusCode.BadRequest, newUser.StatusCode);
        }
    }

    [Fact]
    public async Task Permission_roles_use_the_picker_and_save_instantly()
    {
        var (admin, browser, _, _, permissionId) = await AdminBrowserAsync();
        using (admin) using (browser)
        {
            var page = $"/Admin/Permissions/Assign?id={permissionId}";
            var html = WebUtility.HtmlDecode(await browser.GetStringAsync(page));
            Assert.Contains($"data-instant-url=\"/Admin/Permissions/Assign?id={permissionId}&handler=Role\"", html);
            Assert.DoesNotContain("ti-device-floppy", html);                                              // دکمه‌ی ذخیره ندارد

            var role = (await client.GetFromJsonAsync<List<RoleDto>>("/api/admin/roles"))!.First(r => r.RoleCode == "MEMBER");
            var added = await PostJsonWithToken(browser, $"{page}&handler=Role", page, new { kind = "role", id = role.RoleId, granted = true });
            Assert.Equal(HttpStatusCode.OK, added.StatusCode);
            Assert.Contains(role.RoleId, (await client.GetFromJsonAsync<PermissionDto>($"/api/admin/permissions/{permissionId}"))!.SelectedRoleIds);
            Assert.Equal(HttpStatusCode.OK, (await PostJsonWithToken(browser, $"{page}&handler=Role", page, new { kind = "role", id = role.RoleId, granted = false })).StatusCode);
            Assert.DoesNotContain(role.RoleId, (await client.GetFromJsonAsync<PermissionDto>($"/api/admin/permissions/{permissionId}"))!.SelectedRoleIds);
        }
    }

    [Fact]
    public async Task Picker_script_filters_by_search_shows_radio_rows_and_saves_instantly_with_a_toast()
    {
        var (admin, browser, _, _, _) = await AdminBrowserAsync();
        using (admin) using (browser)
        {
            var js = await browser.GetStringAsync("/assets/js/admin-pickers.js");
            Assert.Contains("initPicker", js);
            Assert.Contains("aria-expanded", js);                                                         // ARIA combobox
            Assert.Contains("type = 'radio'", js);                                                        // موارد انتخاب‌شده رادیوباتن
            Assert.Contains("radio.checked = true", js);
            Assert.Contains("RequestVerificationToken", js);                                              // ثبت لحظه‌ای با antiforgery
            Assert.Contains("window.showToast", js);                                                      // و toast
            Assert.Contains("normalize(", js);                                                            // جست‌وجوی فارسی (ی/ک عربی، ارقام)
            Assert.Contains("ArrowDown", js);                                                             // کار با صفحه‌کلید
            Assert.Contains("data-picker-toggle", js);                                                    // کلیک فلش فهرست را باز و بسته می‌کند
            Assert.Contains("initIconPicker", js);
            Assert.Contains("createElementNS", js);                                                       // خود آیکن با SVG ساخته می‌شود
            Assert.DoesNotContain(".innerHTML", js);                                                       // هیچ innerHTML با داده‌ی کاربر
            Assert.DoesNotContain("insertAdjacentHTML", js);
            var layout = await browser.GetStringAsync("/Admin");
            Assert.Contains("/assets/js/admin-pickers.js", layout);
            var css = await browser.GetStringAsync("/assets/css/admin-ui.css");
            Assert.Contains(".entity-picker", css);
            Assert.Contains(".icon-picker", css);
        }
    }
}
