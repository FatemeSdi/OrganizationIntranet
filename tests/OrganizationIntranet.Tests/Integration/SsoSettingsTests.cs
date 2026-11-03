extern alias AdminEntry;
using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrganizationIntranet.Api.Data;
using OrganizationIntranet.Application.Services;
using OrganizationIntranet.Contracts;
using Xunit;
using P = OrganizationIntranet.Contracts.AdminPermissions;

namespace OrganizationIntranet.Tests;

// تنظیمات SSO هر سامانه (migration 013) از پنل مدیریت: مسیر و کلید در بانک، کلید فقط به‌صورت هش و فقط یک بار قابل دیدن.
public sealed partial class PortalSmokeTests
{
    private Task<HttpResponseMessage> SaveSso(int appId, string? path, string action = SsoKeyActions.Keep, string? newKey = null) =>
        client.PostAsJsonAsync($"/api/admin/applications/{appId}/sso", new SsoSettingsRequest(path, action, newKey));

    private async Task<int> PlainApplication(string code, string baseUrl)
    {
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var app = new OrganizationIntranet.Domain.Entities.Application { ApplicationName = code, ApplicationCode = code, BaseUrl = baseUrl, IsActive = true };
        db.Applications.Add(app); db.SaveChanges();
        return app.ApplicationId;
    }

    [Fact]
    public async Task A_generated_sso_key_is_shown_once_stored_only_as_a_hash_and_works_for_redeeming()
    {
        var app = await PlainApplication("NEWAPP", "https://new.example/");
        await Login();
        var before = (await client.GetFromJsonAsync<SsoSettingsDto>($"/api/admin/applications/{app}/sso"))!;
        Assert.False(before.Enabled);
        Assert.False(before.HasKey);

        var saved = await SaveSso(app, "  Account/Sso ", SsoKeyActions.Generate);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var result = (await saved.Content.ReadFromJsonAsync<SsoSettingsResultDto>())!;
        Assert.Matches("^[A-Za-z0-9_-]{43}$", result.GeneratedKey);
        Assert.True(result.Settings.Enabled);
        Assert.Equal("Account/Sso", result.Settings.SsoPath);
        Assert.Equal("https://new.example/Account/Sso", result.Settings.LandingUrl);

        var again = await client.GetStringAsync($"/api/admin/applications/{app}/sso");                  // خواندن بعدی کلید ندارد
        Assert.DoesNotContain(result.GeneratedKey!, again);
        using (var scope = api.Services.CreateScope())
        {
            var stored = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Applications.AsNoTracking().SingleAsync(a => a.ApplicationId == app);
            Assert.Equal(SsoService.KeyHash(result.GeneratedKey!), stored.SsoKeyHash);                    // فقط هش
            Assert.NotNull(stored.SsoKeyChangedAt);
        }

        using (var scope = api.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.ApplicationAccessGrants.Add(new OrganizationIntranet.Domain.Entities.ApplicationAccess { ApplicationId = app, UserId = UserId("member"), CreatedAt = DateTime.UtcNow }); db.SaveChanges();
        }
        await Login("member");
        var ticket = await StartSso(app);
        using var server = ApplicationServer(result.GeneratedKey!);
        Assert.Equal(HttpStatusCode.OK, (await Redeem(server, "NEWAPP", ticket)).StatusCode);

        await Login();                                                                                   // ساخت دوباره، کلید قبلی را باطل می‌کند
        var second = (await (await SaveSso(app, "Account/Sso", SsoKeyActions.Generate)).Content.ReadFromJsonAsync<SsoSettingsResultDto>())!;
        Assert.NotEqual(result.GeneratedKey, second.GeneratedKey);
        await Login("member");
        var next = await StartSso(app);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Redeem(server, "NEWAPP", next)).StatusCode);
        using var fresh = ApplicationServer(second.GeneratedKey!);
        Assert.Equal(HttpStatusCode.OK, (await Redeem(fresh, "NEWAPP", next)).StatusCode);
    }

    [Fact]
    public async Task An_existing_key_can_be_registered_and_unsafe_or_shared_keys_are_rejected()
    {
        var app = await PlainApplication("APP1", "https://one.example/");
        var other = await PlainApplication("APP2", "https://two.example/");
        await Login();
        const string existing = "existing-application-key-0000000000000000";
        Assert.Equal(HttpStatusCode.OK, (await SaveSso(app, "Account/Sso", SsoKeyActions.Set, "  " + existing + "  ")).StatusCode);   // فاصله‌ی دو سر حذف می‌شود

        Assert.Equal(HttpStatusCode.BadRequest, (await SaveSso(other, "Account/Sso", SsoKeyActions.Set, existing)).StatusCode);                 // کلید سامانه‌ی دیگر
        Assert.Equal(HttpStatusCode.BadRequest, (await SaveSso(other, "Account/Sso", SsoKeyActions.Set, "too-short")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await SaveSso(other, "Account/Sso", SsoKeyActions.Set, "has space in it 0000000000000000000000000")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await SaveSso(other, "Account/Sso", SsoKeyActions.Set, ApiFactory.ClientKey)).StatusCode);       // کلید داخلی UI
        Assert.Equal(HttpStatusCode.BadRequest, (await SaveSso(other, "Account/Sso", SsoKeyActions.Set, ApiFactory.AdminClientKey)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await SaveSso(other, "Account/Sso", "bogus")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SaveSso(99999, "Account/Sso", SsoKeyActions.Generate)).StatusCode);
    }

    [Fact]
    public async Task Sso_path_must_be_relative_and_removing_the_key_or_path_disables_sso()
    {
        var app = await PlainApplication("PATHAPP", "https://path.example/");
        await Login();
        foreach (var bad in new[] { "https://evil.example/x", "//evil.example", "../up", "a\\b", "a#b", new string('a', 201) })
            Assert.Equal(HttpStatusCode.BadRequest, (await SaveSso(app, bad, SsoKeyActions.Generate)).StatusCode);
        Assert.False((await client.GetFromJsonAsync<SsoSettingsDto>($"/api/admin/applications/{app}/sso"))!.HasKey);   // ردشده‌ها هیچ کلیدی نساختند

        Assert.Equal(HttpStatusCode.OK, (await SaveSso(app, "Account/Sso", SsoKeyActions.Generate)).StatusCode);
        var removedKey = (await (await SaveSso(app, "Account/Sso", SsoKeyActions.Remove)).Content.ReadFromJsonAsync<SsoSettingsResultDto>())!.Settings;
        Assert.False(removedKey.HasKey);
        Assert.False(removedKey.Enabled);
        await SaveSso(app, "Account/Sso", SsoKeyActions.Generate);
        var clearedPath = (await (await SaveSso(app, "  ", SsoKeyActions.Keep)).Content.ReadFromJsonAsync<SsoSettingsResultDto>())!.Settings;
        Assert.True(clearedPath.HasKey);
        Assert.Null(clearedPath.SsoPath);
        Assert.False(clearedPath.Enabled);
    }

    [Fact]
    public async Task Sso_settings_need_application_permissions_and_inactive_applications_cannot_redeem()
    {
        var app = await PlainApplication("PERMAPP", "https://perm.example/");
        GrantMember("APP_READER", P.IntranetAdmin, P.ApplicationView);
        await Login("member");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/admin/applications/{app}/sso")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SaveSso(app, "Account/Sso", SsoKeyActions.Generate)).StatusCode);
        GrantMember("APP_EDITOR", P.ApplicationEdit);
        var key = (await (await SaveSso(app, "Account/Sso", SsoKeyActions.Generate)).Content.ReadFromJsonAsync<SsoSettingsResultDto>())!.GeneratedKey!;

        using (var scope = api.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Applications.Single(a => a.ApplicationId == app).IsActive = false; db.SaveChanges();
        }
        using var server = ApplicationServer(key);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Redeem(server, "PERMAPP", new string('a', 43))).StatusCode);       // سامانه‌ی غیرفعال کلیدش هم کار نمی‌کند
    }

    [Fact]
    public async Task Sso_admin_page_shows_a_generated_key_once_and_never_on_reload()
    {
        var app = await PlainApplication("PAGEAPP", "https://page.example/");
        var (admin, browser, _, _, _) = await AdminBrowserAsync();
        using (admin) using (browser)
        {
            var page = WebUtility.HtmlDecode(await browser.GetStringAsync($"/Admin/Applications/Sso?id={app}"));
            Assert.Contains("کلید ثبت نشده", page);
            Assert.DoesNotContain("فقط همین یک بار", page);

            var token = await Antiforgery(browser, $"/Admin/Applications/Sso?id={app}");
            var posted = await browser.PostAsync($"/Admin/Applications/Sso?id={app}&handler=Generate", new FormUrlEncodedContent(new Dictionary<string, string>
            { ["Id"] = app.ToString(), ["SsoPath"] = "Account/Sso", ["__RequestVerificationToken"] = token }));
            var html = WebUtility.HtmlDecode(await posted.Content.ReadAsStringAsync());
            Assert.Equal(HttpStatusCode.OK, posted.StatusCode);
            Assert.Contains("فقط همین یک بار", html);
            var key = System.Text.RegularExpressions.Regex.Match(html, "aria-label=\"کلید ساخته‌شده\"[^>]*value=\"([A-Za-z0-9_-]{43})\"|value=\"([A-Za-z0-9_-]{43})\"[^>]*aria-label=\"کلید ساخته‌شده\"");
            Assert.True(key.Success);

            var reloaded = WebUtility.HtmlDecode(await browser.GetStringAsync($"/Admin/Applications/Sso?id={app}"));
            Assert.Contains("کلید ثبت شده", reloaded);
            Assert.DoesNotContain("فقط همین یک بار", reloaded);
            Assert.DoesNotContain(key.Groups[1].Success ? key.Groups[1].Value : key.Groups[2].Value, reloaded);                  // کلید دوباره نمایش داده نمی‌شود
        }
    }

    [Fact]
    public void Committed_settings_hold_no_sso_keys_or_paths_any_more()
    {
        var text = File.ReadAllText(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "OrganizationIntranet.Api", "appsettings.json")));
        Assert.DoesNotContain("SsoKey", text);
        Assert.DoesNotContain("SsoPath", text.Replace("SsoPath مسیر", ""));
    }
}
