extern alias PortalEntry;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OrganizationIntranet.Api.Data;
using OrganizationIntranet.Api.Security;
using OrganizationIntranet.Contracts;
using OrganizationIntranet.Domain.Entities;
using Xunit;

namespace OrganizationIntranet.Tests;

// ورود یکپارچه با بلیت یک‌بارمصرف (docs/sso.md): صدور برای کاربر دارای دسترسی، تحویل فقط با کلید همان سامانه، یک بار و پیش از انقضا.
public sealed partial class PortalSmokeTests
{
    /// <summary>دو سامانه‌ی دارای SSO (SSOAPP و OTHERAPP) با دسترسی مستقیم member.</summary>
    private (int App, int Other) SeedSsoApplications()
    {
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var member = db.Users.Single(u => u.Username == "member");
        var app = new OrganizationIntranet.Domain.Entities.Application { ApplicationName = "تسک", ApplicationCode = "SSOAPP", BaseUrl = "https://tasktrack.example/", SsoPath = "sso?lang=fa", SsoKeyHash = OrganizationIntranet.Application.Services.SsoService.KeyHash(ApiFactory.SsoKey), IsActive = true };
        var other = new OrganizationIntranet.Domain.Entities.Application { ApplicationName = "میز خدمت", ApplicationCode = "OTHERAPP", BaseUrl = "https://its.example/", SsoPath = "Account/Sso", SsoKeyHash = OrganizationIntranet.Application.Services.SsoService.KeyHash(ApiFactory.OtherSsoKey), IsActive = true };
        app.AccessGrants.Add(new ApplicationAccess { UserId = member.UserId, CreatedAt = DateTime.UtcNow });
        other.AccessGrants.Add(new ApplicationAccess { UserId = member.UserId, CreatedAt = DateTime.UtcNow });
        db.Applications.AddRange(app, other);
        db.SaveChanges();
        return (app.ApplicationId, other.ApplicationId);
    }

    private async Task<string> StartSso(int appId)
    {
        var response = await client.PostAsync($"/api/portal/applications/{appId}/sso", null);
        response.EnsureSuccessStatusCode();
        var url = (await response.Content.ReadFromJsonAsync<SsoStartDto>())!.Url;
        return url[(url.IndexOf("ticket=", StringComparison.Ordinal) + 7)..];
    }

    /// <summary>کلاینت سرور سامانه‌ی مقصد: کلید داخلی UI و توکن کاربر ندارد، فقط کلید SSO خودش.</summary>
    private HttpClient ApplicationServer(string key)
    {
        var server = api.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        server.DefaultRequestHeaders.Add("X-Application-Key", key);
        return server;
    }
    private static Task<HttpResponseMessage> Redeem(HttpClient server, string code, string ticket) =>
        server.PostAsJsonAsync($"/api/integrations/applications/{code}/sso/redeem", new SsoRedeemRequest(ticket));

    [Fact]
    public async Task Sso_ticket_is_issued_for_a_granted_application_and_redeemed_once_by_that_application()
    {
        var (app, _) = SeedSsoApplications();
        await Login("member");
        var apps = (await client.GetFromJsonAsync<List<PortalApplicationDto>>("/api/portal/applications"))!;
        Assert.True(apps.Single(a => a.ApplicationId == app).SingleSignOn);

        var start = (await (await client.PostAsync($"/api/portal/applications/{app}/sso", null)).Content.ReadFromJsonAsync<SsoStartDto>())!;
        Assert.Matches(@"^https://tasktrack\.example/sso\?lang=fa&ticket=[A-Za-z0-9_-]{43}$", start.Url); // پارامتر قبلی آدرس حفظ می‌شود
        var ticket = start.Url[(start.Url.IndexOf("ticket=", StringComparison.Ordinal) + 7)..];

        using var server = ApplicationServer(ApiFactory.SsoKey);
        var redeemed = await Redeem(server, "SSOAPP", ticket);
        Assert.Equal(HttpStatusCode.OK, redeemed.StatusCode);
        var identity = (await redeemed.Content.ReadFromJsonAsync<SsoIdentityDto>())!;
        Assert.Equal("member", identity.Username);                                          // کلید تطبیق: نام کاربری
        Assert.Equal("2222222222", identity.NationalId);                                         // کد ملی فقط اطلاع‌رسانی است

        var again = await Redeem(server, "SSOAPP", ticket);                                     // یک‌بارمصرف
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
        Assert.Equal(SingleSignOn.InvalidTicket, (await again.Content.ReadFromJsonAsync<OperationResult>())!.Message);

        using var scope = api.Services.CreateScope();
        var stored = scope.ServiceProvider.GetRequiredService<AppDbContext>().SsoTickets.AsNoTracking().Single();
        Assert.Equal(SHA256.HashData(Encoding.ASCII.GetBytes(ticket)), stored.TicketHash);        // خود بلیت ذخیره نمی‌شود
        Assert.NotNull(stored.RedeemedAt);
        Assert.Equal(SingleSignOn.TicketSeconds, (stored.ExpiresAt - stored.CreatedAt).TotalSeconds, 3);
    }

    [Fact]
    public async Task Sso_redeem_requires_the_applications_own_key_and_rejects_tickets_of_other_applications()
    {
        var (app, other) = SeedSsoApplications();
        await Login("member");
        var ticket = await StartSso(app);

        using (var anonymous = api.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") }))
            Assert.Equal(HttpStatusCode.Unauthorized, (await Redeem(anonymous, "SSOAPP", ticket)).StatusCode);
        foreach (var (key, code) in new[] { ("wrong-key-0000000000000000000000000000000", "SSOAPP"), (ApiFactory.IntegrationKey, "TEST"), (ApiFactory.IntegrationKey, "SSOAPP"), (ApiFactory.OtherSsoKey, "SSOAPP") })
        {
            using var server = ApplicationServer(key);
            Assert.Equal(HttpStatusCode.Unauthorized, (await Redeem(server, code, ticket)).StatusCode); // کلید اعلان یا کلید سامانه‌ی دیگر کافی نیست
        }
        using (var otherServer = ApplicationServer(ApiFactory.OtherSsoKey))
            Assert.Equal(HttpStatusCode.BadRequest, (await Redeem(otherServer, "OTHERAPP", ticket)).StatusCode); // بلیت SSOAPP به OTHERAPP داده نمی‌شود
        using (var server = ApplicationServer(ApiFactory.SsoKey))
        {
            Assert.Equal(HttpStatusCode.OK, (await Redeem(server, "SSOAPP", ticket)).StatusCode); // تلاش‌های ناموفق بلیت را نسوزانده‌اند
            foreach (var bad in new[] { "", "short", new string('a', 44), ticket[..42] + "!" })
                Assert.Equal(HttpStatusCode.BadRequest, (await Redeem(server, "SSOAPP", bad)).StatusCode);
        }
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/portal/applications/{other}/sso", null)).StatusCode);
    }

    [Fact]
    public async Task Sso_ticket_expires_and_honours_access_and_account_changes_made_after_the_click()
    {
        var (app, _) = SeedSsoApplications();
        await Login("member");
        using var server = ApplicationServer(ApiFactory.SsoKey);
        void Change(Action<AppDbContext> change)
        {
            using var scope = api.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            change(db); db.SaveChanges();
        }

        var expired = await StartSso(app);
        Change(db => { var t = db.SsoTickets.Single(); t.CreatedAt = DateTime.UtcNow.AddMinutes(-3); t.ExpiresAt = DateTime.UtcNow.AddMinutes(-2); });
        Assert.Equal(HttpStatusCode.BadRequest, (await Redeem(server, "SSOAPP", expired)).StatusCode);

        var revoked = await StartSso(app);
        Change(db => db.ApplicationAccessGrants.RemoveRange(db.ApplicationAccessGrants.Where(g => g.ApplicationId == app)));
        Assert.Equal(HttpStatusCode.BadRequest, (await Redeem(server, "SSOAPP", revoked)).StatusCode);   // دسترسی بعد از کلیک گرفته شد
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync($"/api/portal/applications/{app}/sso", null)).StatusCode);

        Change(db => db.ApplicationAccessGrants.Add(new ApplicationAccess { ApplicationId = app, UserId = db.Users.Single(u => u.Username == "member").UserId, CreatedAt = DateTime.UtcNow }));
        var disabled = await StartSso(app);
        Change(db => db.Users.Single(u => u.Username == "member").IsActive = false);
        Assert.Equal(HttpStatusCode.BadRequest, (await Redeem(server, "SSOAPP", disabled)).StatusCode);  // کاربر غیرفعال شد
    }

    [Fact]
    public async Task Sso_works_without_a_national_id_and_start_explains_applications_without_sso()
    {
        var (app, _) = SeedSsoApplications();
        using (var scope = api.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Applications.Single(a => a.ApplicationCode == "TEST").IsPublic = true; // دسترسی دارد ولی SSO ندارد
            db.SaveChanges();
        }
        await Login("member");
        var testApp = (await client.GetFromJsonAsync<List<PortalApplicationDto>>("/api/portal/applications"))!.Single(a => a.Name == "سامانه");
        Assert.False(testApp.SingleSignOn);
        var noSso = await client.PostAsync($"/api/portal/applications/{testApp.ApplicationId}/sso", null);
        Assert.Equal(HttpStatusCode.BadRequest, noSso.StatusCode);
        Assert.Contains("فعال نیست", (await noSso.Content.ReadFromJsonAsync<OperationResult>())!.Message);

        using (var scope = api.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Users.Single(u => u.Username == "member").NationalId = null;
            db.SaveChanges();
        }
        var start = await client.PostAsync($"/api/portal/applications/{app}/sso", null);        // نبود کد ملی مانع نیست؛ کلید نام کاربری است
        Assert.Equal(HttpStatusCode.OK, start.StatusCode);
        var url = (await start.Content.ReadFromJsonAsync<SsoStartDto>())!.Url;
        using var server = ApplicationServer(ApiFactory.SsoKey);
        var redeemed = await Redeem(server, "SSOAPP", url[(url.IndexOf("ticket=", StringComparison.Ordinal) + 7)..]);
        Assert.Equal(HttpStatusCode.OK, redeemed.StatusCode);
        var identity = (await redeemed.Content.ReadFromJsonAsync<SsoIdentityDto>())!;
        Assert.Equal("member", identity.Username);
        Assert.Null(identity.NationalId);
    }

    [Fact]
    public async Task Concurrent_redeems_of_one_ticket_let_exactly_one_application_request_through()
    {
        var (app, _) = SeedSsoApplications();
        await Login("member");
        var ticket = await StartSso(app);
        using var server = ApplicationServer(ApiFactory.SsoKey);
        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Redeem(server, "SSOAPP", ticket)));
        // اتصال SQLite درون‌حافظه‌ی آزمون بین درخواست‌ها مشترک است و درخواست هم‌زمان را سریالی نمی‌کند (بقیه ممکن است ۵۰۰ شوند)؛
        // تضمین واقعی روی SQL Server با اتصال‌های جدا در FreshInstallTests آزموده می‌شود. اینجا فقط: هرگز دو بار ۲۰۰.
        Assert.Equal(1, results.Count(r => r.StatusCode == HttpStatusCode.OK));
    }

    [Fact]
    public async Task Portal_cards_of_sso_applications_open_with_one_click()
    {
        var (app, _) = SeedSsoApplications();
        var token = await Login("member");
        using var portal = Ui<PortalEntry::Program>("Portal");
        using var browser = portal.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        browser.DefaultRequestHeaders.Add("Cookie", Cookie(portal.Services, token, false));

        var home = WebUtility.HtmlDecode(await browser.GetStringAsync("/Portal"));
        Assert.Contains($"href=\"/Sso/Open?app={app}\"", home);
        Assert.Contains("ورود به سامانه", home);
        foreach (var technical in new[] { "فقط اینترانت", "قابل دسترسی از اینترنت", "اختصاص‌یافته", "ورود با یک کلیک", "ورود با لاگین", "ورود بدون نیاز به لاگین" })
            Assert.DoesNotContain(technical, home);                                                          // جزئیات فنی روی کارت نمی‌آید
        Assert.DoesNotContain("href=\"https://tasktrack.example/\"", home);

        var open = await browser.GetAsync($"/Sso/Open?app={app}");
        Assert.Equal(HttpStatusCode.Redirect, open.StatusCode);
        Assert.Matches(@"^https://tasktrack\.example/sso\?lang=fa&ticket=[A-Za-z0-9_-]{43}$", open.Headers.Location!.OriginalString);

        var denied = await browser.GetAsync("/Sso/Open?app=99999");
        Assert.Equal(HttpStatusCode.BadRequest, denied.StatusCode);
        Assert.Contains("این سامانه برای شما در دسترس نیست", WebUtility.HtmlDecode(await denied.Content.ReadAsStringAsync()));

        using var anonymous = portal.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var challenge = await anonymous.GetAsync($"/Sso/Open?app={app}");
        Assert.Equal(HttpStatusCode.Redirect, challenge.StatusCode);
        Assert.Contains("/Account/Login", challenge.Headers.Location!.OriginalString);
    }


    [Theory]
    [InlineData("Security/Account/Sso", "https://host:5671/", "https://host:5671/Security/Account/Sso")]
    [InlineData("/Security/Account/Sso", "https://host:5671", "https://host:5671/Security/Account/Sso")]        // اسلش اول و آخر مهم نیست
    [InlineData("Account/Sso", "http://localhost:62100/", "http://localhost:62100/Account/Sso")]
    [InlineData("Account/Sso", "https://host/app/", "https://host/app/Account/Sso")]                       // سامانه‌ای که زیر یک مسیر میزبانی شده
    [InlineData("Account/Sso", "https://host/?x=1", "https://host/Account/Sso")]                           // query نشانی ورود به آدرس SSO نمی‌چسبد
    [InlineData("sso?lang=fa", "https://host/", "https://host/sso?lang=fa")]                               // query خود مسیر حفظ می‌شود
    [InlineData("Account/Sso", null, null)]                                                              // بدون نشانی ورود در پنل آدرس ساخته نمی‌شود
    [InlineData("Account/Sso", "not a url", null)]
    [InlineData("Account/Sso", "ftp://host/", null)]
    [InlineData("//evil.example/x", "https://host/", null)]                                                  // مسیر نمی‌تواند میزبان را عوض کند
    [InlineData("https://evil.example/x", "https://host/", null)]
    [InlineData("../../admin", "https://host/app/", null)]
    [InlineData("a\\b", "https://host/", null)]
    [InlineData("a#b", "https://host/", null)]
    [InlineData("", "https://host/", null)]
    [InlineData(null, "https://host/", null)]
    public void Sso_landing_url_is_built_from_the_panel_base_url_and_a_relative_path_on_the_same_host(string? path, string? baseUrl, string? expected) =>
        Assert.Equal(expected, OrganizationIntranet.Application.Services.SsoLanding.Url(baseUrl, path));

    [Fact]
    public void Sso_is_enabled_only_when_a_key_is_registered()
    {
        Assert.Equal("https://host/Account/Sso", OrganizationIntranet.Application.Services.SsoLanding.Url("https://host/", "Account/Sso", hasKey: true));
        Assert.Null(OrganizationIntranet.Application.Services.SsoLanding.Url("https://host/", "Account/Sso", hasKey: false));
    }
}
