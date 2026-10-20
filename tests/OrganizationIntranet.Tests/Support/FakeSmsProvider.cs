using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace OrganizationIntranet.Tests;

/// <summary>
/// شبیه‌ساز پنل پیامک داخلی سازمان روی loopback برای آزمون بدون شبکه‌ی داخلی.
/// قرارداد: GET /api/SMSProvider?appName=&amp;to=&amp;msg= ؛ پاسخ متنی «success» یا خطا.
/// فقط نام برنامه‌ی ثبت‌شده پذیرفته می‌شود. هیچ پیامکی به بیرون نمی‌رود.
/// </summary>
public sealed partial class FakeSmsProvider : IAsyncDisposable
{
    public const string RegisteredApp = "OrganizationIntranet";
    public sealed record ReceivedSms(string RawQuery, string AppName, string To, string Message);

    public List<ReceivedSms> Messages { get; } = new();
    public int Calls { get; private set; }
    /// <summary>برای شبیه‌سازی رفتار غیرعادی درگاه (redirect، خطا، پاسخ ناموفق)؛ در حالت عادی null است.</summary>
    public Func<HttpContext, IResult>? Override { get; set; }
    public string BaseUrl { get; private set; } = "";
    private WebApplication app = null!;

    [GeneratedRegex(@"^\+?[0-9]{10,15}$")]
    private static partial Regex Mobile();

    public static async Task<FakeSmsProvider> StartAsync()
    {
        var provider = new FakeSmsProvider();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Test" });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        provider.app = builder.Build();
        provider.app.MapGet("/api/SMSProvider", provider.Handle);
        await provider.app.StartAsync();
        provider.BaseUrl = provider.app.Urls.First().TrimEnd('/') + "/api/SMSProvider";
        return provider;
    }

    private IResult Handle(HttpContext context)
    {
        Calls++;
        if (Override is not null) return Override(context);
        var query = context.Request.Query;
        string appName = query["appName"].ToString(), to = query["to"].ToString(), message = query["msg"].ToString();
        if (appName != RegisteredApp) return Results.Text("error: application is not registered");
        if (!Mobile().IsMatch(to)) return Results.Text("error: invalid mobile number");
        if (string.IsNullOrWhiteSpace(message)) return Results.Text("error: empty message");
        Messages.Add(new(context.Request.QueryString.Value ?? "", appName, to, message));
        return Results.Text("success");
    }

    public async ValueTask DisposeAsync() { await app.StopAsync(); await app.DisposeAsync(); }
}
