namespace OrganizationIntranet.Application.Abstractions;

/// <summary>درگاه پیامک داخلی سازمان. در نبود پیکربندی معتبر، IsConfigured برابر false است و هیچ تماسی گرفته نمی‌شود.</summary>
public interface ISmsGateway
{
    bool IsConfigured { get; }
    /// <summary>در هر شکست (پیکربندی، شماره، پاسخ درگاه یا شبکه) استثنا می‌دهد؛ شماره و متن پیام در پیام خطا نمی‌آیند.</summary>
    Task SendAsync(string mobile, string message, CancellationToken cancellationToken);
}
