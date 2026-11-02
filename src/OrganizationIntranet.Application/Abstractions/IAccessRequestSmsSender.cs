namespace OrganizationIntranet.Application.Abstractions;

/// <summary>ارسال پیامک اطلاع‌رسانی درخواست دسترسی به ناظران.</summary>
public interface IAccessRequestSmsSender
{
    /// <summary>یک پیامک می‌فرستد؛ idempotencyKey ارسال تکراری را در تلاش دوباره می‌گیرد.</summary>
    Task SendAsync(string mobile, string message, string idempotencyKey, CancellationToken cancellationToken);
}
