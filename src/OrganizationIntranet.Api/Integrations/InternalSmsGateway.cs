using System.Text.RegularExpressions;
using OrganizationIntranet.Application.Abstractions;

namespace OrganizationIntranet.Api.Integrations;

public static class InternalSmsGatewayRegistration
{
    // ثبت واحد برای برنامه و آزمون‌ها: timeout پانزده‌ثانیه‌ای و بدون دنبال‌کردن redirect یا کوکی.
    public static IServiceCollection AddInternalSmsGateway(this IServiceCollection services)
    {
        services.AddHttpClient<ISmsGateway, InternalSmsGateway>(client => client.Timeout = TimeSpan.FromSeconds(15))
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false });
        return services;
    }
}

/// <summary>
/// کلاینت پنل پیامک داخلی سازمان (همان درگاهی که IdentityManagementSystem استفاده می‌کند).
/// قرارداد درگاه: GET با appName/to/msg در query و بدون کلید؛ موفقیت یعنی HTTP 2xx و وجود واژه «success» در پاسخ متنی.
/// تفاوت عمدی با نمونه مرجع: مقدارها یک‌بار کامل percent-encode می‌شوند و بعد decode نمی‌شوند؛ پس متنی که «&amp;»، «#» یا «+»
/// دارد نمی‌تواند پارامترهای درخواست را عوض کند. برای متن فارسی، بایت‌های ارسالی با نمونه مرجع یکسان است.
/// تنظیمات فقط از پیکربندی امن API می‌آید (Sms:BaseUrl و Sms:AppName)، نه از ورودی کاربر یا دیتابیس.
/// درگاه Idempotency-Key ندارد؛ بنابراین تحویل در شرایط قطع ارتباط at-least-once است.
/// </summary>
public sealed partial class InternalSmsGateway : ISmsGateway
{
    private readonly HttpClient client;
    private readonly ILogger<InternalSmsGateway> logger;
    private readonly Uri? baseUrl;
    private readonly string appName;

    public InternalSmsGateway(HttpClient client, IConfiguration configuration, ILogger<InternalSmsGateway> logger)
    {
        this.client = client;
        this.logger = logger;
        appName = configuration["Sms:AppName"]?.Trim() ?? "";
        if (Uri.TryCreate(configuration["Sms:BaseUrl"]?.Trim(), UriKind.Absolute, out var url)
            && url.Scheme is "http" or "https" && url.UserInfo.Length == 0 && url.Query.Length == 0 && url.Fragment.Length == 0)
            baseUrl = url;
    }

    // نام برنامه باید در پنل پیامک ثبت شده باشد؛ بدون آن عمداً غیرفعال می‌ماند تا با نام برنامه دیگری ارسال نشود.
    public bool IsConfigured => baseUrl is not null && appName.Length > 0;

    [GeneratedRegex(@"^\+?[0-9]{10,15}$")]
    private static partial Regex MobilePattern();

    public async Task SendAsync(string mobile, string message, CancellationToken cancellationToken)
    {
        if (!IsConfigured) throw new InvalidOperationException("Internal SMS gateway is not configured.");
        if (!MobilePattern().IsMatch(mobile ?? "")) throw new ArgumentException("SMS recipient is not a valid mobile number.");
        if (string.IsNullOrWhiteSpace(message)) throw new ArgumentException("SMS message is empty.");

        var url = $"{baseUrl!.GetLeftPart(UriPartial.Path)}?appName={Uri.EscapeDataString(appName)}&to={Uri.EscapeDataString(mobile!)}&msg={Uri.EscapeDataString(message)}";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        using var response = await client.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode || !body.Contains("success", StringComparison.OrdinalIgnoreCase))
        {
            // شماره، متن پیام و پاسخ درگاه عمداً ثبت نمی‌شوند.
            logger.LogWarning("Internal SMS gateway rejected the request (HTTP {Status})", (int)response.StatusCode);
            throw new InvalidOperationException("Internal SMS gateway rejected the request.");
        }
    }
}
