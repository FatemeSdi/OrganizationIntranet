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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrganizationIntranet.Api.Data;
using OrganizationIntranet.Application.Abstractions;
using OrganizationIntranet.Contracts;
using Xunit;

namespace OrganizationIntranet.Tests;

// ورود با کد ملی و پیامک از صفحه‌ی ورود واقعی Portal و Admin، با درگاه پیامک جعلی (هیچ پیامک واقعی ارسال نمی‌شود).
public sealed partial class PortalSmokeTests
{
    private sealed class CapturingSmsGateway : ISmsGateway
    {
        public List<string> Messages { get; } = new();
        public bool IsConfigured => true;
        public Task SendAsync(string mobile, string message, CancellationToken cancellationToken) { Messages.Add(message); return Task.CompletedTask; }
        public string LastCode => Regex.Match(Messages[^1], "[0-9]{6}").Value;
    }

    private async Task<(WebApplicationFactory<Program> Api, CapturingSmsGateway Sms)> NationalIdApiAsync()
    {
        var sms = new CapturingSmsGateway();
        var apiWithSms = api.WithWebHostBuilder(b => b.ConfigureServices(s => { s.RemoveAll<ISmsGateway>(); s.AddSingleton<ISmsGateway>(sms); }));
        using (var scope = apiWithSms.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Users.Single(u => u.Username == "member").NationalId = "0012345679";
            db.SaveChanges();
        }
        using var admin = apiWithSms.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        admin.DefaultRequestHeaders.Add("X-Intranet-Client", ApiFactory.ClientKey);
        admin.DefaultRequestHeaders.Add("X-Intranet-Admin", ApiFactory.AdminClientKey);
        var login = await admin.PostAsJsonAsync("/api/account/login", new LoginRequest("admin", "test-password"));
        using (var json = JsonDocument.Parse(await login.Content.ReadAsStringAsync()))
            admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", json.RootElement.GetProperty("accessToken").GetString());
        var settings = (await admin.GetFromJsonAsync<AuthenticationSettingsDto>("/api/admin/authentication"))!;
        settings.SmsEnabled = true; settings.NationalIdSmsEnabled = true; // درگاه داخلی (جعلی) فعال است، پس نشانی HTTPS لازم نیست
        (await admin.PostAsJsonAsync("/api/admin/authentication", settings)).EnsureSuccessStatusCode();
        return (apiWithSms, sms);
    }

    private static WebApplicationFactory<T> UiFor<T>(string name, WebApplicationFactory<Program> apiHost) where T : class => new WebApplicationFactory<T>().WithWebHostBuilder(builder =>
    {
        builder.UseSetting("Api:ClientKey", ApiFactory.ClientKey);
        builder.UseSetting("Ui:Name", name);
        builder.UseSetting("Sites:Admin", "https://localhost");
        builder.UseSetting("Sites:Portal", "https://localhost");
        if (name == "Admin") builder.UseSetting("Api:AdminClientKey", ApiFactory.AdminClientKey);
        builder.ConfigureServices(services =>
        {
            services.AddHttpClient("ApiClient").ConfigurePrimaryHttpMessageHandler(() => apiHost.Server.CreateHandler());
            services.AddSingleton<IDNTCaptchaValidatorService, AcceptedCaptcha>();
        });
    });

    private static async Task<JsonElement> PostLogin(HttpClient browser, string handler, Dictionary<string, string> fields)
    {
        fields["__RequestVerificationToken"] = await Antiforgery(browser, "/Account/Login");
        var response = await browser.PostAsync("/Account/Login?handler=" + handler, new FormUrlEncodedContent(fields));
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
    }

    [Fact]
    public async Task Portal_login_dropdown_signs_in_with_national_id_and_sms_code()
    {
        var (apiWithSms, sms) = await NationalIdApiAsync();
        using var portal = UiFor<PortalEntry::Program>("Portal", apiWithSms);
        using var browser = portal.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });

        var page = WebUtility.HtmlDecode(await browser.GetStringAsync("/Account/Login"));
        Assert.Contains("value=\"national-id-sms\"", page);
        Assert.Contains("کد ملی و پیامک", page);
        Assert.Contains("data-method-panel=\"national-id\"", page);
        Assert.Contains("login-form.js", page);
        Assert.Contains("logo-pmo.png", page);               // لوگوی اداره
        Assert.Contains("supportModal", page);               // پشتیبانی
        Assert.DoesNotContain("پنل مدیریت اینترانت سازمان", page); // ظاهر جدا از ورود مدیریت

        var sent = await PostLogin(browser, "SendCode", new() { ["Method"] = "national-id-sms", ["NationalId"] = "۰۰۱۲۳۴۵۶۷۹" });
        Assert.True(sent.GetProperty("success").GetBoolean());
        Assert.DoesNotContain(sent.ToString(), sms.Messages[^1]); // کد هرگز به مرورگر برنمی‌گردد
        var code = sms.LastCode;
        Assert.DoesNotContain(code, sent.ToString());

        var wrong = await PostLogin(browser, "VerifyCode", new() { ["Code"] = code == "000000" ? "111111" : "000000" });
        Assert.False(wrong.GetProperty("success").GetBoolean());

        var done = await PostLogin(browser, "VerifyCode", new() { ["Code"] = code });
        Assert.True(done.GetProperty("success").GetBoolean());
        Assert.EndsWith("/Portal", done.GetProperty("redirectUrl").GetString());
        Assert.Contains("/Account/Logout", await browser.GetStringAsync("/Portal")); // نشست کاربر ساخته شد
    }

    [Fact]
    public async Task Verify_without_a_pending_national_id_login_asks_to_restart()
    {
        var (apiWithSms, _) = await NationalIdApiAsync();
        using var admin = UiFor<AdminEntry::Program>("Admin", apiWithSms);
        using var browser = admin.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var page = WebUtility.HtmlDecode(await browser.GetStringAsync("/Account/Login"));
        Assert.Contains("value=\"national-id-sms\"", page); // همان dropdown در ورود مدیریت
        var result = await PostLogin(browser, "VerifyCode", new() { ["Code"] = "123456" });
        Assert.False(result.GetProperty("success").GetBoolean());
        Assert.True(result.GetProperty("restart").GetBoolean());
    }
}
