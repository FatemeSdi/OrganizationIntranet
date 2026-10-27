extern alias AdminEntry;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using OrganizationIntranet.Api.Data;
using OrganizationIntranet.Contracts;
using OrganizationIntranet.Domain.Entities;
using Xunit;

namespace OrganizationIntranet.Tests;

// نگاشت کاربر اینترانت به نام کاربری او در هر سامانه (docs/sso.md): همه‌ی کار در اینترانت است و سامانه‌ها تغییری نمی‌کنند.
public sealed partial class PortalSmokeTests
{
    private long UserId(string username)
    {
        using var scope = api.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.Single(u => u.Username == username).UserId;
    }

    private long AddUser(string username)
    {
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = new User { Username = username, Name = "آزمون", LastName = "کاربر", IsActive = true, CreatedAt = DateTime.UtcNow };
        db.Users.Add(user);
        db.SaveChanges();
        return user.UserId;
    }

    private Task<HttpResponseMessage> MapAccount(long userId, int applicationId, string? name) =>
        client.PostAsJsonAsync($"/api/admin/users/{userId}/sso-accounts/{applicationId}", new SsoAccountRequest(name));

    private async Task<string> RedeemedUsername(string applicationCode, int applicationId, string key)
    {
        await Login("member");
        var ticket = await StartSso(applicationId);
        using var server = ApplicationServer(key);
        var response = await Redeem(server, applicationCode, ticket);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<SsoIdentityDto>())!.Username;
    }

    [Fact]
    public async Task Sso_gives_the_application_the_mapped_username_and_falls_back_to_the_intranet_username()
    {
        var (app, other) = SeedSsoApplications();
        var memberId = UserId("member");
        Assert.Equal("member", await RedeemedUsername("SSOAPP", app, ApiFactory.SsoKey));              // بدون نگاشت: همان نام کاربری اینترانت

        await Login("admin");
        var saved = await MapAccount(memberId, app, "  Task.User  ");                                   // فاصله‌ی دو سر حذف می‌شود
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var accounts = (await saved.Content.ReadFromJsonAsync<SsoAccountsDto>())!;
        Assert.Equal("Task.User", accounts.Accounts.Single(a => a.ApplicationId == app).ExternalUsername);
        Assert.Null(accounts.Accounts.Single(a => a.ApplicationId == other).ExternalUsername);

        Assert.Equal("Task.User", await RedeemedUsername("SSOAPP", app, ApiFactory.SsoKey));           // سامانه همین نام را می‌گیرد
        Assert.Equal("member", await RedeemedUsername("OTHERAPP", other, ApiFactory.OtherSsoKey));      // سامانه‌ی دیگر دست‌نخورده است

        await Login("admin");
        Assert.Equal(HttpStatusCode.OK, (await MapAccount(memberId, app, "Task.User2")).StatusCode);   // ویرایش
        Assert.Equal("Task.User2", await RedeemedUsername("SSOAPP", app, ApiFactory.SsoKey));
        await Login("admin");
        Assert.Equal(HttpStatusCode.OK, (await MapAccount(memberId, app, "  ")).StatusCode);           // خالی = حذف نگاشت
        Assert.Equal("member", await RedeemedUsername("SSOAPP", app, ApiFactory.SsoKey));

        using var scope = api.Services.CreateScope();
        Assert.Empty(scope.ServiceProvider.GetRequiredService<AppDbContext>().UserApplicationAccounts);
    }

    [Fact]
    public async Task An_application_account_belongs_to_one_intranet_user_only()
    {
        var (app, other) = SeedSsoApplications();
        var memberId = UserId("member");
        var adminId = UserId("admin");
        var third = AddUser("third.user");
        await Login("admin");

        Assert.Equal(HttpStatusCode.OK, (await MapAccount(memberId, app, "shared.account")).StatusCode);
        var duplicate = await MapAccount(third, app, "SHARED.account");                                 // حساب یکسان (بدون فرق حروف) برای کاربر دیگر
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        Assert.Contains("قبلاً", (await duplicate.Content.ReadFromJsonAsync<OperationResult>())!.Message);
        Assert.Equal(HttpStatusCode.OK, (await MapAccount(third, other, "shared.account")).StatusCode);  // در سامانه‌ی دیگر مجاز است

        Assert.Equal(HttpStatusCode.BadRequest, (await MapAccount(third, app, "admin")).StatusCode);     // نام کاربری اینترانت کاربر دیگری که نگاشت ندارد
        Assert.Equal(HttpStatusCode.OK, (await MapAccount(adminId, app, "admin.in.app")).StatusCode);    // admin نگاشت دارد، پس نام «admin» آزاد می‌شود
        Assert.Equal(HttpStatusCode.OK, (await MapAccount(third, app, "admin")).StatusCode);
    }

    [Fact]
    public async Task Sso_refuses_the_default_name_when_another_user_is_mapped_to_that_account()
    {
        var (app, _) = SeedSsoApplications();
        var third = AddUser("third.user");
        await Login("admin");
        Assert.Equal(HttpStatusCode.BadRequest, (await MapAccount(third, app, "member")).StatusCode);    // از راه API نمی‌شود: «member» نام کاربری اینترانت کاربر بدون نگاشت است
        using (var scope = api.Services.CreateScope())                                                   // ولی اگر کاربر «member» بعد از نگاشت ساخته شده باشد (یا مستقیم در بانک)
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.UserApplicationAccounts.Add(new UserApplicationAccount { UserId = third, ApplicationId = app, ExternalUsername = "member", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, UpdatedBy = third });
            db.SaveChanges();                                                                            // third حساب «member» سامانه را دارد
        }

        await Login("member");
        var ticket = await StartSso(app);
        using var server = ApplicationServer(ApiFactory.SsoKey);
        var redeemed = await Redeem(server, "SSOAPP", ticket);                                           // member بدون نگاشت به حساب دیگری نمی‌رسد
        Assert.Equal(HttpStatusCode.BadRequest, redeemed.StatusCode);
        Assert.Equal(SingleSignOn.InvalidTicket, (await redeemed.Content.ReadFromJsonAsync<OperationResult>())!.Message);
    }

    [Fact]
    public async Task Mapping_validates_input_and_is_limited_to_user_editors()
    {
        var (app, _) = SeedSsoApplications();
        var memberId = UserId("member");
        await Login("admin");
        foreach (var bad in new[] { "has space", "tab\tname", new string('a', 101) })
            Assert.Equal(HttpStatusCode.BadRequest, (await MapAccount(memberId, app, bad)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await MapAccount(999999, app, "x")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await MapAccount(memberId, 999999, "x")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/admin/users/{memberId}/sso-accounts")).StatusCode);

        await Login("member");                                                                           // مجوز USER_EDIT و USER_VIEW ندارد
        Assert.Equal(HttpStatusCode.Forbidden, (await MapAccount(memberId, app, "x")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/admin/users/{memberId}/sso-accounts")).StatusCode);
    }

    [Fact]
    public async Task Admin_user_accounts_page_lists_sso_applications_and_saves_the_mapping()
    {
        var (app, _) = SeedSsoApplications();
        var (admin, browser, _, memberId, _) = await AdminBrowserAsync();
        using (admin) using (browser)
        {
            var html = System.Net.WebUtility.HtmlDecode(await browser.GetStringAsync($"/Admin/Users/Accounts?id={memberId}"));
            Assert.Contains("تسک", html);
            Assert.Contains("SSOAPP", html);
            Assert.Contains("placeholder=\"member (پیش‌فرض)\"", html);                                   // نام پیش‌فرض نشان داده می‌شود
            Assert.Contains("href=\"/Admin/Users/Accounts?id=", System.Net.WebUtility.HtmlDecode(await browser.GetStringAsync("/Admin/Users")));

            var form = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Id"] = memberId.ToString(), ["ApplicationId"] = app.ToString(), ["ExternalUsername"] = "ttms.member",
                ["__RequestVerificationToken"] = await Antiforgery(browser, $"/Admin/Users/Accounts?id={memberId}"),
            });
            Assert.Equal(HttpStatusCode.Redirect, (await browser.PostAsync($"/Admin/Users/Accounts?id={memberId}", form)).StatusCode);
            using var scope = api.Services.CreateScope();
            Assert.Equal("ttms.member", scope.ServiceProvider.GetRequiredService<AppDbContext>().UserApplicationAccounts.Single().ExternalUsername);
        }
    }
}
