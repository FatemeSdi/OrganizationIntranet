using System.Net;
using System.Web;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using OrganizationIntranet.Api.Integrations;
using OrganizationIntranet.Api.Security;
using OrganizationIntranet.Application.Abstractions;
using Xunit;

namespace OrganizationIntranet.Tests;

[Trait("Category", "Unit")]
public sealed class InternalSmsGatewayTests
{
    private static InternalSmsGateway Create(RecordingHandler handler, string? baseUrl = "http://sms.internal.test:8083/api/SMSProvider", string? appName = "OrganizationIntranet")
    {
        var settings = new Dictionary<string, string?> { ["Sms:BaseUrl"] = baseUrl, ["Sms:AppName"] = appName };
        return new InternalSmsGateway(new HttpClient(handler), new ConfigurationBuilder().AddInMemoryCollection(settings).Build(), NullLogger<InternalSmsGateway>.Instance);
    }

    private sealed class RecordingHandler(HttpStatusCode status = HttpStatusCode.OK, string body = "success") : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = new();
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
        }
    }

    [Theory]
    [InlineData("http://sms.internal.test:8083/api/SMSProvider", "OrganizationIntranet", true)]
    [InlineData("http://sms.internal.test:8083/api/SMSProvider", "", false)]   // نام برنامه در پنل ثبت نشده
    [InlineData("http://sms.internal.test:8083/api/SMSProvider", null, false)]
    [InlineData(null, "OrganizationIntranet", false)]
    [InlineData("not a url", "OrganizationIntranet", false)]
    [InlineData("ftp://sms.internal.test/api", "OrganizationIntranet", false)]
    [InlineData("http://user:pass@sms.internal.test/api", "OrganizationIntranet", false)]  // credential در URL ممنوع
    [InlineData("http://sms.internal.test/api?appName=other", "OrganizationIntranet", false)]
    public void Configuration_is_validated(string? baseUrl, string? appName, bool expected) =>
        Assert.Equal(expected, Create(new RecordingHandler(), baseUrl, appName).IsConfigured);

    [Fact]
    public async Task Unconfigured_gateway_never_calls_the_network()
    {
        var handler = new RecordingHandler();
        await Assert.ThrowsAsync<InvalidOperationException>(() => Create(handler, appName: "").SendAsync("09121234567", "سلام", default));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Sends_a_GET_with_appName_to_and_msg()
    {
        var handler = new RecordingHandler();
        await Create(handler).SendAsync("09121234567", "کد تأیید اینترانت: 123456\nاعتبار: ۵ دقیقه", default);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("/api/SMSProvider", request.RequestUri!.AbsolutePath);
        var query = HttpUtility.ParseQueryString(request.RequestUri.Query);
        Assert.Equal("OrganizationIntranet", query["appName"]);
        Assert.Equal("09121234567", query["to"]);
        Assert.Equal("کد تأیید اینترانت: 123456\nاعتبار: ۵ دقیقه", query["msg"]);
        Assert.Null(request.Headers.Authorization); // درگاه کلید ندارد؛ اعتبار اینترانت به آن ارسال نمی‌شود.
    }

    [Fact]
    public async Task Message_text_cannot_inject_or_override_query_parameters()
    {
        var handler = new RecordingHandler();
        var hostile = "a&to=09999999999&appName=other#frag +x%26y";
        await Create(handler).SendAsync("09121234567", hostile, default);
        var query = HttpUtility.ParseQueryString(Assert.Single(handler.Requests).RequestUri!.Query);
        Assert.Equal(["appName", "msg", "to"], query.AllKeys.OfType<string>().Order().ToArray());
        Assert.Equal("09121234567", query["to"]);
        Assert.Equal("OrganizationIntranet", query["appName"]);
        Assert.Equal(hostile, query["msg"]);
    }

    [Theory]
    [InlineData("Success", true)]
    [InlineData("{\"status\":\"SUCCESS\"}", true)]
    [InlineData("error: invalid app", false)]
    [InlineData("", false)]
    public async Task Success_requires_the_word_success_in_the_response(string body, bool ok)
    {
        var gateway = Create(new RecordingHandler(HttpStatusCode.OK, body));
        if (ok) await gateway.SendAsync("09121234567", "x", default);
        else await Assert.ThrowsAsync<InvalidOperationException>(() => gateway.SendAsync("09121234567", "x", default));
    }

    [Fact]
    public async Task Non_success_status_fails_even_if_the_body_says_success() =>
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Create(new RecordingHandler(HttpStatusCode.InternalServerError, "success")).SendAsync("09121234567", "x", default));

    [Theory]
    [InlineData("")]
    [InlineData("0912")]
    [InlineData("09121234567&to=1")]
    [InlineData("abc1234567890")]
    public async Task Invalid_recipients_are_rejected_before_any_call(string mobile)
    {
        var handler = new RecordingHandler();
        await Assert.ThrowsAsync<ArgumentException>(() => Create(handler).SendAsync(mobile, "x", default));
        Assert.Empty(handler.Requests);
    }

    private sealed class FakeGateway(bool configured) : ISmsGateway
    {
        public List<(string Mobile, string Message)> Sent { get; } = new();
        public bool IsConfigured => configured;
        public Task SendAsync(string mobile, string message, CancellationToken cancellationToken) { Sent.Add((mobile, message)); return Task.CompletedTask; }
    }

    [Fact]
    public async Task Access_request_sms_uses_the_internal_gateway_when_configured()
    {
        var gateway = new FakeGateway(true);
        // وقتی درگاه داخلی فعال است، تنظیمات HTTPS/Bearer و کلاینت‌های آن نباید لمس شوند (null اگر لمس شوند خطا می‌دهد).
        await new AccessRequestSmsSender(null!, null!, null!, gateway).SendAsync("09121234567", "پیام", "key-1", default);
        Assert.Equal([("09121234567", "پیام")], gateway.Sent);
    }

    [Fact]
    public async Task Login_code_sms_uses_the_internal_gateway_when_configured()
    {
        var gateway = new FakeGateway(true);
        var providers = new AuthenticationProviders(DataProtectionProvider.Create("sms-test"), null!, gateway, NullLogger<AuthenticationProviders>.Instance);
        Assert.True(providers.InternalSmsAvailable);
        await providers.SendSmsAsync(new(), "", "09121234567", "123456");
        var sent = Assert.Single(gateway.Sent);
        Assert.Equal("09121234567", sent.Mobile);
        Assert.Contains("123456", sent.Message);
        Assert.False(new AuthenticationProviders(DataProtectionProvider.Create("sms-test"), null!, new FakeGateway(false), NullLogger<AuthenticationProviders>.Instance).InternalSmsAvailable);
    }

    [Fact]
    public async Task Empty_message_is_rejected() =>
        await Assert.ThrowsAsync<ArgumentException>(() => Create(new RecordingHandler()).SendAsync("09121234567", "  ", default));
}
