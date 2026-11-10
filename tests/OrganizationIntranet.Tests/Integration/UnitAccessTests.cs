extern alias AdminEntry;
using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrganizationIntranet.Api.Data;
using OrganizationIntranet.Contracts;
using OrganizationIntranet.Domain.Entities;
using Xunit;

namespace OrganizationIntranet.Tests;

// دسترسی سامانه بر اساس چارت سازمانی (migration 014): کاربران واحدهای انتخاب‌شده و زیرواحدهایشان؛ بقیه نه می‌بینند و نه درخواست می‌دهند.
public sealed partial class PortalSmokeTests
{
    /// <summary>سامانه‌ی ساده با حالت «محدود به واحد» و واحدهای داده‌شده، مستقیم در بانک.</summary>
    private int UnitApplication(string code, params int[] unitIds)
    {
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var app = new OrganizationIntranet.Domain.Entities.Application { ApplicationName = code, ApplicationCode = code, BaseUrl = "https://" + code.ToLower() + ".example/", IsActive = true, IsUnitRestricted = true };
        foreach (var id in unitIds) app.UnitAccess.Add(new ApplicationUnitAccess { UnitId = id, CreatedAt = DateTime.UtcNow });
        db.Applications.Add(app); db.SaveChanges();
        return app.ApplicationId;
    }

    private void PlaceMember(int? unitId)
    {
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Users.Single(u => u.Username == "member").UnitId = unitId; db.SaveChanges();
    }

    private async Task<List<string>> MemberApplicationNames()
    {
        await Login("member");
        return (await client.GetFromJsonAsync<List<PortalApplicationDto>>("/api/portal/applications"))!.Select(a => a.Name).ToList();
    }

    [Fact]
    public async Task A_unit_restricted_application_is_available_to_the_selected_unit_and_all_its_sub_units_only()
    {
        await Login();
        var root = await AddUnit("اداره کل");
        var tech = await AddUnit("معاونت فنی", root);
        var it = await AddUnit("فناوری اطلاعات", tech);
        var admin = await AddUnit("معاونت اداری", root);
        var similar = await AddUnit("معاونت فنی ۲", root);                       // نام با پیشوند مشابه، نه زیرواحد معاونت فنی
        UnitApplication("UNITAPP", tech);

        PlaceMember(null);
        Assert.DoesNotContain("UNITAPP", await MemberApplicationNames());           // بدون واحد
        PlaceMember(tech);
        Assert.Contains("UNITAPP", await MemberApplicationNames());                 // خود واحد
        PlaceMember(it);
        Assert.Contains("UNITAPP", await MemberApplicationNames());                 // زیرواحد
        PlaceMember(admin);
        Assert.DoesNotContain("UNITAPP", await MemberApplicationNames());           // واحد هم‌سطح
        PlaceMember(root);
        Assert.DoesNotContain("UNITAPP", await MemberApplicationNames());           // واحد بالاتر دسترسی نمی‌گیرد
        PlaceMember(similar);
        Assert.DoesNotContain("UNITAPP", await MemberApplicationNames());           // پیشوند مشابه به‌اشتباه زیرواحد حساب نمی‌شود
    }

    [Fact]
    public async Task Deactivated_units_and_inactive_users_lose_unit_based_access_and_explicit_grants_still_work()
    {
        await Login();
        var tech = await AddUnit("معاونت فنی");
        var it = await AddUnit("فناوری اطلاعات", tech);
        var app = UnitApplication("UNITAPP2", tech);
        PlaceMember(it);
        Assert.Contains("UNITAPP2", await MemberApplicationNames());

        await Login();
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/admin/org/units/{it}/toggle", null)).StatusCode);   // واحد کاربر غیرفعال
        Assert.DoesNotContain("UNITAPP2", await MemberApplicationNames());
        await Login();
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/admin/org/units/{it}/toggle", null)).StatusCode);
        Assert.Contains("UNITAPP2", await MemberApplicationNames());
        await Login();
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/admin/org/units/{tech}/toggle", null)).StatusCode);  // واحد انتخاب‌شده (و زیرواحدش) غیرفعال
        Assert.DoesNotContain("UNITAPP2", await MemberApplicationNames());

        PlaceMember(null);                                                                                         // خارج از واحد، اما با دسترسی مستقیم
        using (var scope = api.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.ApplicationAccessGrants.Add(new ApplicationAccess { ApplicationId = app, UserId = UserId("member"), CreatedAt = DateTime.UtcNow }); db.SaveChanges();
        }
        Assert.Contains("UNITAPP2", await MemberApplicationNames());
    }

    [Fact]
    public async Task Unit_restricted_applications_are_hidden_from_request_lists_and_sso_for_outsiders()
    {
        await Login();
        var tech = await AddUnit("معاونت فنی");
        var app = UnitApplication("UNITAPP3", tech);
        PlaceMember(null);
        await Login("member");
        var requestable = (await client.GetFromJsonAsync<List<RequestableApplicationDto>>("/api/portal/access-requests/applications"))!;
        Assert.DoesNotContain(requestable, a => a.ApplicationId == app);                                         // قابل درخواست نیست
        var request = await client.PostAsJsonAsync("/api/portal/access-requests", new CreateAccessRequest(app, "دلیل کافی برای دسترسی"));
        Assert.Equal(HttpStatusCode.BadRequest, request.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync($"/api/portal/applications/{app}/sso", null)).StatusCode);

        PlaceMember(tech);
        await Login("member");
        Assert.DoesNotContain((await client.GetFromJsonAsync<List<RequestableApplicationDto>>("/api/portal/access-requests/applications"))!, a => a.ApplicationId == app);   // و برای عضو واحد هم نیازی نیست
        Assert.Contains("UNITAPP3", (await client.GetFromJsonAsync<List<PortalApplicationDto>>("/api/portal/applications"))!.Select(a => a.Name));
    }

    [Fact]
    public async Task Admin_saves_the_unit_access_mode_with_rules_for_units_and_the_public_flag()
    {
        await Login();
        var tech = await AddUnit("معاونت فنی");
        var off = await AddUnit("کنار", null, active: false);
        var app = await PlainApplication("MODEAPP", "https://mode.example/");
        var current = (await client.GetFromJsonAsync<ApplicationConfigurationDto>($"/api/admin/applications/{app}"))!;
        Assert.False(current.Application.IsUnitRestricted);

        Task<HttpResponseMessage> Save(bool isPublic, bool unitMode, List<int>? units) => client.PostAsJsonAsync($"/api/admin/applications/{app}",
            new ApplicationConfigurationRequest(new ApplicationRequest("MODEAPP", "MODEAPP", "https://mode.example/", null, 1, "apps", true, false, isPublic, "InApp", unitMode), [], [], [], units));

        Assert.Equal(HttpStatusCode.BadRequest, (await Save(true, true, [tech])).StatusCode);                   // عمومی و واحدی هم‌زمان نمی‌شود
        Assert.Equal(HttpStatusCode.BadRequest, (await Save(false, true, [])).StatusCode);                      // بدون واحد
        Assert.Equal(HttpStatusCode.BadRequest, (await Save(false, true, null)).StatusCode);                    // و بدون واحد قبلی
        Assert.Equal(HttpStatusCode.BadRequest, (await Save(false, true, [99999])).StatusCode);                 // ناموجود
        Assert.Equal(HttpStatusCode.BadRequest, (await Save(false, true, [off])).StatusCode);                   // غیرفعال
        Assert.Equal(HttpStatusCode.OK, (await Save(false, true, [tech, tech])).StatusCode);                    // تکراری یکی می‌شود
        var saved = (await client.GetFromJsonAsync<ApplicationConfigurationDto>($"/api/admin/applications/{app}"))!;
        Assert.True(saved.Application.IsUnitRestricted);
        Assert.Equal([tech], saved.UnitIds);

        Assert.Equal(HttpStatusCode.OK, (await Save(false, true, null)).StatusCode);                            // null = واحدهای قبلی بدون تغییر
        Assert.Equal([tech], (await client.GetFromJsonAsync<ApplicationConfigurationDto>($"/api/admin/applications/{app}"))!.UnitIds);
        Assert.Equal(HttpStatusCode.OK, (await Save(false, false, null)).StatusCode);                           // برگشت به حالت درخواستی؛ فهرست واحدها حفظ می‌شود
        var back = (await client.GetFromJsonAsync<ApplicationConfigurationDto>($"/api/admin/applications/{app}"))!;
        Assert.False(back.Application.IsUnitRestricted);
        Assert.Equal([tech], back.UnitIds);
        Assert.Equal(HttpStatusCode.BadRequest, (await Save(false, true, [])).StatusCode);                      // پاک‌کردن همه‌ی واحدها در حالت واحدی ممکن نیست
    }

    [Fact]
    public async Task Unit_access_rows_cannot_point_to_missing_units_and_deleting_a_used_unit_is_not_possible()
    {
        await Login();
        var tech = await AddUnit("معاونت فنی");
        UnitApplication("UNITAPP4", tech);
        var delete = await client.PostAsync($"/api/admin/org/units/{tech}/delete", null);
        Assert.NotEqual(HttpStatusCode.OK, delete.StatusCode);                                                    // واحد در دسترسی یک سامانه استفاده شده
        using var scope = api.Services.CreateScope();
        Assert.True(await scope.ServiceProvider.GetRequiredService<AppDbContext>().OrgUnits.AnyAsync(u => u.UnitId == tech));
    }

    [Fact]
    public async Task Admin_application_page_offers_the_three_access_modes_and_saves_the_unit_mode()
    {
        await Login();
        var tech = await AddUnit("معاونت فنی");
        var app = await PlainApplication("PAGEMODE", "https://pagemode.example/");
        var (admin, browser, _, _, _) = await AdminBrowserAsync();
        using (admin) using (browser)
        {
            var html = WebUtility.HtmlDecode(await browser.GetStringAsync($"/Admin/Applications/Edit?id={app}"));
            Assert.Contains("بر اساس چارت سازمانی", html);
            Assert.Contains("با تخصیص یا درخواست", html);
            Assert.Contains("name=\"Input.UnitIds\"", html);
            Assert.Contains("معاونت فنی", html);
        }
    }
}
