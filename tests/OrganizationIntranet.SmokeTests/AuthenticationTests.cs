extern alias PortalEntry;
extern alias AdminEntry;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using DNTCaptcha.Core;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrganizationIntranet.Api.Data;
using OrganizationIntranet.Api.Security;
using OrganizationIntranet.Application.Abstractions;
using OrganizationIntranet.Contracts;
using OrganizationIntranet.Domain.Entities;
using OtpNet;
using Xunit;

namespace OrganizationIntranet.SmokeTests;

public sealed class AuthenticationTests : IDisposable
{
    private readonly ApiFactory original = new();
    private readonly WebApplicationFactory<Program> api;
    private readonly TestProviders providers = new();
    private readonly HttpClient client;
    private const string Secret = "JBSWY3DPEHPK3PXP";
    public AuthenticationTests()
    {
        api = original.WithWebHostBuilder(b => b.ConfigureServices(s => {
            s.RemoveAll<IAuthenticationProviders>();
            s.AddSingleton<IAuthenticationProviders>(providers);
        }));
        client = api.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Intranet-Client", ApiFactory.ClientKey);
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.EnsureCreated();
        var role = new Role { RoleCode = "ADMIN", RoleName = "مدیر", IsActive = true };
        foreach (var name in new[] { "admin", "member" })
        {
            var u = new User { Username = name, Name = name, LastName = "Test", MobileNumber = "09120000000", IsActive = true };
            u.PasswordHash = new PasswordService().Hash(u, "test-password");
            if (name == "admin") u.UserRoles.Add(new UserRole { Role = role });
            db.Users.Add(u);
        }
        db.SaveChanges();
    }
    private sealed record Tokens(string AccessToken, string RefreshToken);
    private async Task<Tokens> Login(string username = "admin", string method = "password", string password = "test-password")
    {
        var result = await client.PostAsJsonAsync("/api/account/login", new LoginRequest(username, password, method));
        result.EnsureSuccessStatusCode();
        var token = (await result.Content.ReadFromJsonAsync<Tokens>())!;
        Assert.False(string.IsNullOrEmpty(token.AccessToken));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        return token;
    }
    private async Task<AuthenticationSettingsDto> Settings() => (await client.GetFromJsonAsync<AuthenticationSettingsDto>("/api/admin/authentication"))!;
    private async Task Save(AuthenticationSettingsDto settings) => (await client.PostAsJsonAsync("/api/admin/authentication", settings)).EnsureSuccessStatusCode();
    private void Mutate(Action<AppDbContext> action)
    {
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        action(db); db.SaveChanges();
    }
    private async Task<AuthenticationChallengeDto> Challenge(string username = "admin")
    {
        var response = await client.PostAsJsonAsync("/api/account/login", new LoginRequest(username, "test-password"));
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("accessToken", json);
        return JsonSerializer.Deserialize<AuthenticationChallengeDto>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
    }
    private async Task EnableSms(bool twoFactor = true, bool reset = false)
    {
        await Login();
        var s = await Settings();
        s.SmsEnabled = true; s.SmsEndpoint = "https://sms.example.org/send"; s.SmsApiKey = "private-test-key";
        s.TwoFactorEnabled = twoFactor; s.ForgotPasswordEnabled = reset;
        await Save(s);
    }
    private static string Code(string secret = Secret) => new Totp(Base32Encoding.ToBytes(secret)).ComputeTotp();

    [Fact]
    public async Task Defaults_and_server_switches_block_disabled_methods_and_empty_password_activation()
    {
        var methods = (await client.GetFromJsonAsync<AuthenticationMethodsDto>("/api/account/methods"))!;
        Assert.True(methods.PasswordEnabled);
        Assert.False(methods.LdapEnabled || methods.SmsEnabled || methods.TwoFactorEnabled || methods.ForgotPasswordEnabled);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/admin/authentication")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/account/login", new LoginRequest("admin", "test-password", "ldap"))).StatusCode);
        Assert.Equal(0, providers.LdapCalls);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/account/forgot-password", new ForgotPasswordRequest("admin"))).StatusCode);
        await Login("member");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admin/authentication")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/admin/authentication", new AuthenticationSettingsDto())).StatusCode);
        Mutate(db => db.Users.Single(u => u.Username == "member").PasswordHash = null);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/account/login", new LoginRequest("member", "attacker-password"))).StatusCode);
    }

    [Fact]
    public async Task Settings_validate_dependencies_mask_secrets_and_invalidate_sessions()
    {
        var tokens = await Login();
        var s = await Settings();
        s.ForgotPasswordEnabled = true;
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/admin/authentication", s)).StatusCode);
        s.ForgotPasswordEnabled = false; s.TwoFactorEnabled = true; s.AuthenticatorEnabled = false;
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/admin/authentication", s)).StatusCode);
        s.TwoFactorEnabled = false; s.SmsEnabled = true; s.SmsEndpoint = "http://sms.example.org"; s.SmsApiKey = "secret";
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/admin/authentication", s)).StatusCode);
        s.SmsEndpoint = "https://sms.example.org/send";
        await Save(s);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/account/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/account/refresh", new RefreshRequest(tokens.RefreshToken))).StatusCode);
        await Login();
        var masked = await Settings();
        Assert.Null(masked.SmsApiKey); Assert.True(masked.HasSmsApiKey);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/admin/authentication", s)).StatusCode);
        await Save(masked); // Empty key preserves stored credential.
        Mutate(db => Assert.StartsWith("protected:", db.Set<AuthenticationSettings>().Single().ProtectedSmsApiKey));
    }

    [Fact]
    public async Task Ldap_requires_existing_active_account_and_verified_session_before_disabling_local_login()
    {
        await Login(); var s = await Settings();
        s.LdapEnabled = true; s.LdapHost = "dc.example.org"; s.LdapDomain = "example.org";
        await Save(s);
        await Login(); s = await Settings(); s.PasswordEnabled = false;
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/admin/authentication", s)).StatusCode);
        await Login(method: "ldap", password: "directory-password");
        await Save(s);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/account/login", new LoginRequest("admin", "test-password"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/account/login", new LoginRequest("unknown", "directory-password", "ldap"))).StatusCode);
        Mutate(db => db.Users.Single(u => u.Username == "member").IsActive = false);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/account/login", new LoginRequest("member", "directory-password", "ldap"))).StatusCode);
        Assert.Equal(1, providers.LdapCalls);
    }

    [Fact]
    public async Task Authenticator_enrollment_requires_password_and_code_and_prevents_replay()
    {
        var old = await Login();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/account/authenticator/setup", new AuthenticatorSetupRequest("wrong"))).StatusCode);
        var setupResponse = await client.PostAsJsonAsync("/api/account/authenticator/setup", new AuthenticatorSetupRequest("test-password"));
        setupResponse.EnsureSuccessStatusCode();
        var setup = (await setupResponse.Content.ReadFromJsonAsync<AuthenticatorSetupDto>())!;
        Assert.StartsWith("otpauth://totp/", setup.ProvisioningUri);
        Assert.False((await client.GetFromJsonAsync<AuthenticationStatusDto>("/api/account/security"))!.AuthenticatorConfigured);
        (await client.PostAsJsonAsync("/api/account/authenticator/confirm", new ConfirmAuthenticatorRequest(setup.ChallengeToken, Code(setup.Secret)))).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/account/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/account/refresh", new RefreshRequest(old.RefreshToken))).StatusCode);
        await Login();
        Assert.True((await client.GetFromJsonAsync<AuthenticationStatusDto>("/api/account/security"))!.AuthenticatorConfigured);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/account/authenticator/confirm", new ConfirmAuthenticatorRequest(setup.ChallengeToken, Code(setup.Secret)))).StatusCode);
        var s = await Settings(); s.TwoFactorEnabled = true; await Save(s);
        var challenge = await Challenge();
        Assert.Equal(new[] { "authenticator" }, challenge.Methods);
        // Enrollment already consumed the current time step.
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/account/verify-login", new VerifyLoginRequest(challenge.ChallengeToken, "authenticator", Code(setup.Secret)))).StatusCode);
        Mutate(db => db.Users.Single(u => u.Username == "admin").LastAuthenticatorStep = -1);
        var verified = await client.PostAsJsonAsync("/api/account/verify-login", new VerifyLoginRequest(challenge.ChallengeToken, "authenticator", Code(setup.Secret)));
        verified.EnsureSuccessStatusCode();
        Assert.NotNull((await verified.Content.ReadFromJsonAsync<Tokens>())!.AccessToken);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/account/verify-login", new VerifyLoginRequest(challenge.ChallengeToken, "authenticator", Code(setup.Secret)))).StatusCode);
        var another = await Challenge();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/account/verify-login", new VerifyLoginRequest(another.ChallengeToken, "authenticator", Code(setup.Secret)))).StatusCode);
    }

    [Fact]
    public async Task Sms_is_bound_to_challenge_has_cooldown_and_is_consumed_once()
    {
        await EnableSms();
        var c = await Challenge();
        (await client.PostAsJsonAsync("/api/account/send-login-sms", new SendLoginSmsRequest(c.ChallengeToken))).EnsureSuccessStatusCode();
        Assert.Equal(6, providers.LastCode!.Length);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/account/send-login-sms", new SendLoginSmsRequest(c.ChallengeToken))).StatusCode);
        var other = await Challenge();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/account/send-login-sms", new SendLoginSmsRequest(other.ChallengeToken))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/account/verify-login", new VerifyLoginRequest(other.ChallengeToken, "sms", providers.LastCode))).StatusCode);
        (await client.PostAsJsonAsync("/api/account/verify-login", new VerifyLoginRequest(c.ChallengeToken, "sms", providers.LastCode))).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/account/verify-login", new VerifyLoginRequest(c.ChallengeToken, "sms", providers.LastCode))).StatusCode);
        Assert.Equal(1, providers.SmsCalls);
    }

    [Fact]
    public async Task Expired_challenges_and_five_wrong_codes_cannot_authenticate()
    {
        await EnableSms(); var c = await Challenge();
        (await client.PostAsJsonAsync("/api/account/send-login-sms", new SendLoginSmsRequest(c.ChallengeToken))).EnsureSuccessStatusCode();
        Mutate(db => db.Set<AuthenticationChallenge>().Single().ExpiresAt = DateTime.UtcNow.AddMinutes(-1));
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/account/verify-login", new VerifyLoginRequest(c.ChallengeToken, "sms", providers.LastCode!))).StatusCode);
        c = await Challenge();
        for (var i = 0; i < 5; i++)
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/account/verify-login", new VerifyLoginRequest(c.ChallengeToken, "sms", "invalid"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/account/login", new LoginRequest("admin", "test-password"))).StatusCode);
        Mutate(db => Assert.True(db.Users.Single(u => u.Username == "admin").LockedUntil > DateTime.UtcNow));
    }

    [Fact]
    public async Task Recovery_requires_sms_not_identity_fields_and_revokes_tokens()
    {
        await EnableSms(twoFactor: false, reset: true);
        var old = await Login();
        var response = await client.PostAsJsonAsync("/api/account/forgot-password", new ForgotPasswordRequest("admin"));
        var c = (await response.Content.ReadFromJsonAsync<AuthenticationChallengeDto>())!;
        var unknown = await client.PostAsJsonAsync("/api/account/forgot-password", new ForgotPasswordRequest("unknown"));
        var fake = (await unknown.Content.ReadFromJsonAsync<AuthenticationChallengeDto>())!;
        Assert.Equal(c.ChallengeToken.Length, fake.ChallengeToken.Length);
        Assert.Equal(c.Methods, fake.Methods); Assert.Equal(c.ExpiresIn, fake.ExpiresIn);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/account/reset-password", new CompletePasswordResetRequest(c.ChallengeToken, "invalid", "new-password", "new-password"))).StatusCode);
        var reset = new CompletePasswordResetRequest(c.ChallengeToken, providers.LastCode!, "new-password", "new-password");
        (await client.PostAsJsonAsync("/api/account/reset-password", reset)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/account/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/account/refresh", new RefreshRequest(old.RefreshToken))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/account/reset-password", reset)).StatusCode);
        await Login(password: "new-password");
        Assert.Equal(1, providers.SmsCalls);
    }

    [Fact]
    public async Task Concurrent_challenge_consumption_and_totp_step_updates_conflict()
    {
        await EnableSms(); await Challenge();
        using var scope1 = api.Services.CreateScope(); using var scope2 = api.Services.CreateScope();
        var a = scope1.ServiceProvider.GetRequiredService<AppDbContext>();
        var b = scope2.ServiceProvider.GetRequiredService<AppDbContext>();
        var ca = await a.Set<AuthenticationChallenge>().SingleAsync();
        var cb = await b.Set<AuthenticationChallenge>().SingleAsync();
        ca.Consumed = cb.Consumed = true;
        await scope1.ServiceProvider.GetRequiredService<IAuthenticationRepository>().SaveAsync();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => scope2.ServiceProvider.GetRequiredService<IAuthenticationRepository>().SaveAsync());
        b.ChangeTracker.Clear();
        var ua = await a.Users.SingleAsync(u => u.Username == "admin");
        var ub = await b.Users.SingleAsync(u => u.Username == "admin");
        ua.LastAuthenticatorStep = ub.LastAuthenticatorStep = 123;
        await scope1.ServiceProvider.GetRequiredService<IAuthenticationRepository>().SaveAsync();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => scope2.ServiceProvider.GetRequiredService<IAuthenticationRepository>().SaveAsync());
    }

    private WebApplicationFactory<T> Ui<T>(string name) where T : class => new WebApplicationFactory<T>().WithWebHostBuilder(b => {
        b.UseSetting("Api:ClientKey", ApiFactory.ClientKey); b.UseSetting("Ui:Name", name);
        b.ConfigureServices(s => {
            s.AddHttpClient("ApiClient").ConfigurePrimaryHttpMessageHandler(() => api.Server.CreateHandler());
            s.AddSingleton<IDNTCaptchaValidatorService, AcceptedCaptcha>();
        });
    });
    private sealed class AcceptedCaptcha : IDNTCaptchaValidatorService { public bool HasRequestValidCaptchaEntry() => true; }
    private static async Task<string> Csrf(HttpClient browser, string path)
    {
        var html = await browser.GetStringAsync(path);
        return WebUtility.HtmlDecode(Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
    }
    [Theory]
    [InlineData("Portal")]
    [InlineData("Admin")]
    public async Task UI_second_factor_does_not_issue_cookie_early_and_preserves_destination(string name)
    {
        await EnableSms();
        using var portal = Ui<PortalEntry::Program>("Portal");
        using var admin = Ui<AdminEntry::Program>("Admin");
        using var browser = name == "Portal" ? portal.CreateClient(new() { BaseAddress = new("https://localhost"), AllowAutoRedirect = false }) : admin.CreateClient(new() { BaseAddress = new("https://localhost"), AllowAutoRedirect = false });
        var csrf = await Csrf(browser, "/Account/Login");
        var response = await browser.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string,string> {
            ["__RequestVerificationToken"] = csrf, ["Username"] = "admin", ["Password"] = "test-password", ["RememberMe"] = "true", ["ReturnUrl"] = "/Account/Profile"
        }));
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("/Account/TwoFactor", json.RootElement.GetProperty("redirectUrl").GetString());
        Assert.DoesNotContain(response.Headers.GetValues("Set-Cookie"), c => c.StartsWith("OrganizationIntranet.UI="));
        Assert.Equal(HttpStatusCode.Redirect, (await browser.GetAsync("/Account/Profile")).StatusCode);
        csrf = await Csrf(browser, "/Account/TwoFactor");
        await Fixture(browser, name, "/Account/TwoFactor", "twofactor");
        Assert.Equal(HttpStatusCode.BadRequest, (await browser.PostAsync("/Account/TwoFactor?handler=SendSms", new FormUrlEncodedContent([]))).StatusCode);
        (await browser.PostAsync("/Account/TwoFactor?handler=SendSms", new FormUrlEncodedContent(new Dictionary<string,string> { ["__RequestVerificationToken"] = csrf }))).EnsureSuccessStatusCode();
        response = await browser.PostAsync("/Account/TwoFactor", new FormUrlEncodedContent(new Dictionary<string,string> {
            ["__RequestVerificationToken"] = csrf, ["Method"] = "sms", ["Code"] = providers.LastCode!
        }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Account/Profile", response.Headers.Location!.ToString());
        Assert.Contains(response.Headers.GetValues("Set-Cookie"), c => c.StartsWith("OrganizationIntranet.UI=") && c.Contains("expires="));
        (await browser.GetAsync("/Account/Profile")).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task UI_settings_form_and_security_setup_render_and_validate_antiforgery()
    {
        using var admin = Ui<AdminEntry::Program>("Admin");
        using var browser = admin.CreateClient(new() { BaseAddress = new("https://localhost"), AllowAutoRedirect = false });
        var csrf = await Csrf(browser, "/Account/Login");
        (await browser.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string,string> {
            ["__RequestVerificationToken"] = csrf, ["Username"] = "admin", ["Password"] = "test-password"
        }))).EnsureSuccessStatusCode();
        await Fixture(browser, "Admin", "/Account/Login", "login");
        var html = await Fixture(browser, "Admin", "/Admin/Authentication", "settings");
        Assert.Contains("Input_TwoFactorEnabled", html);
        Assert.Contains("Input_LdapEnabled", html);
        Assert.Contains("Input_SmsApiKey", html);
        var revision = WebUtility.HtmlDecode(Regex.Match(html, "name=\"Input.Revision\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
        csrf = await Csrf(browser, "/Admin/Authentication");
        var form = new Dictionary<string,string> { ["Input.Revision"] = revision, ["Input.PasswordEnabled"] = "true",
            ["Input.AuthenticatorEnabled"] = "true", ["Input.LdapPort"] = "636", ["Input.LdapHost"] = "", ["Input.LdapDomain"] = "", ["Input.SmsEndpoint"] = "", ["Input.SmsSender"] = "" };
        Assert.Equal(HttpStatusCode.BadRequest, (await browser.PostAsync("/Admin/Authentication", new FormUrlEncodedContent(form))).StatusCode);
        form["__RequestVerificationToken"] = csrf;
        var response = await browser.PostAsync("/Admin/Authentication", new FormUrlEncodedContent(form));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Account/Login", response.Headers.Location!.ToString());
        csrf = await Csrf(browser, "/Account/Login");
        (await browser.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string,string> {
            ["__RequestVerificationToken"] = csrf, ["Username"] = "admin", ["Password"] = "test-password"
        }))).EnsureSuccessStatusCode();
        await Fixture(browser, "Admin", "/Account/Security", "security");
        csrf = await Csrf(browser, "/Account/Security");
        response = await browser.PostAsync("/Account/Security", new FormUrlEncodedContent(new Dictionary<string,string> {
            ["__RequestVerificationToken"] = csrf, ["Password"] = "test-password", ["LoginMethod"] = "password"
        }));
        response.EnsureSuccessStatusCode();
        html = await Fixture(browser, "Admin", "/Account/Security", "enrollment");
        Assert.Contains(Secret, html); // Generated test-only secret; the production key never enters these fixtures.
    }

    [Theory]
    [InlineData("Portal")]
    [InlineData("Admin")]
    public async Task UI_recovery_round_trip_uses_sms_and_all_forms_have_named_controls(string name)
    {
        await EnableSms(twoFactor: false, reset: true);
        using var portal = Ui<PortalEntry::Program>("Portal");
        using var admin = Ui<AdminEntry::Program>("Admin");
        using var browser = name == "Portal" ? portal.CreateClient(new() { BaseAddress = new("https://localhost"), AllowAutoRedirect = false }) : admin.CreateClient(new() { BaseAddress = new("https://localhost"), AllowAutoRedirect = false });
        await Fixture(browser, name, "/Account/Login", "login");
        var html = await Fixture(browser, name, "/Account/ForgotPassword", "forgot");
        Assert.Contains("name=\"Username\"", html);
        var csrf = await Csrf(browser, "/Account/ForgotPassword");
        var response = await browser.PostAsync("/Account/ForgotPassword", new FormUrlEncodedContent(new Dictionary<string,string> {
            ["__RequestVerificationToken"] = csrf, ["Username"] = "member"
        }));
        response.EnsureSuccessStatusCode();
        html = await Fixture(browser, name, "/Account/ForgotPassword", "reset");
        Assert.Contains("name=\"Code\"", html);
        csrf = await Csrf(browser, "/Account/ForgotPassword");
        response = await browser.PostAsync("/Account/ForgotPassword?handler=Reset", new FormUrlEncodedContent(new Dictionary<string,string> {
            ["__RequestVerificationToken"] = csrf, ["Code"] = providers.LastCode!, ["NewPassword"] = "recovered-password", ["ConfirmNewPassword"] = "recovered-password"
        }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Account/Login", response.Headers.Location!.ToString());
        await Login("member", password: "recovered-password");
    }

    private static async Task<string> Fixture(HttpClient browser, string host, string route, string name)
    {
        var html = await browser.GetStringAsync(route);
        var root = Environment.GetEnvironmentVariable("INTRANET_AUTH_FIXTURES");
        if (string.IsNullOrEmpty(root)) return html;
        var folder = Path.Combine(root, host, name);
        Directory.CreateDirectory(folder);
        // Capture CAPTCHA images from the test server as well, so responsive review uses fully rendered inputs.
        foreach (Match match in Regex.Matches(html, "<img[^>]+src=\"([^\"]+)\""))
        {
            var source = WebUtility.HtmlDecode(match.Groups[1].Value);
            if (!source.Contains("DNTCaptcha", StringComparison.OrdinalIgnoreCase)) continue;
            using var response = await browser.GetAsync(source);
            if (!response.IsSuccessStatusCode) continue;
            await File.WriteAllBytesAsync(Path.Combine(folder, "captcha.png"), await response.Content.ReadAsByteArrayAsync());
            html = html.Replace(match.Groups[1].Value, "/" + host + "/" + name + "/captcha.png");
        }
        await File.WriteAllTextAsync(Path.Combine(folder, "index.html"), html);
        return html;
    }

    private sealed class TestProviders : IAuthenticationProviders
    {
        public string? LastCode { get; private set; }
        public int SmsCalls { get; private set; }
        public int LdapCalls { get; private set; }
        public string Protect(string value) => "protected:" + value;
        public string Unprotect(string value) => value["protected:".Length..];
        public string NewAuthenticatorSecret() => Secret;
        public bool VerifyAuthenticator(string secret, string code, out long step) => new Totp(Base32Encoding.ToBytes(secret)).VerifyTotp(code, out step, new VerificationWindow(1, 1));
        public Task<bool> VerifyLdapAsync(AuthenticationSettingsDto settings, string username, string password) { LdapCalls++; return Task.FromResult(password == "directory-password"); }
        public Task SendSmsAsync(AuthenticationSettingsDto settings, string protectedApiKey, string mobile, string code) { LastCode = code; SmsCalls++; return Task.CompletedTask; }
        public void Audit(string action, long? userId, bool success) { }
    }
    public void Dispose() { client.Dispose(); api.Dispose(); original.Dispose(); }
}
