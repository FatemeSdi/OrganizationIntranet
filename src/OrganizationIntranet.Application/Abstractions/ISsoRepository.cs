using OrganizationIntranet.Domain.Entities;

namespace OrganizationIntranet.Application.Abstractions;

/// <summary>سامانه‌ای که کاربر الان به آن دسترسی دارد، با کد ملی کاربر.</summary>
public sealed record SsoTarget(int ApplicationId, string ApplicationCode, long UserId);

public interface ISsoRepository
{
    /// <summary>null یعنی سامانه فعال نیست یا کاربر (فعال) به آن دسترسی ندارد.</summary>
    Task<SsoTarget?> TargetAsync(long userId, int applicationId);
    void AddTicket(SsoTicket ticket);
    /// <summary>
    /// بلیت را به‌صورت اتمیک مصرف می‌کند (فقط یک بار، فقط پیش از انقضا، فقط برای همان سامانه) و اگر کاربر هنوز فعال است و دسترسی دارد
    /// کاربر را برمی‌گرداند. بلیت نامعتبر یا کاربری که دسترسی‌اش رفته null می‌دهد.
    /// </summary>
    Task<User?> RedeemAsync(byte[] ticketHash, string applicationCode, DateTime now);
    Task DeleteExpiredAsync(DateTime before);
    Task SaveAsync();
}

/// <summary>تنظیمات SSO هر سامانه (در تنظیمات امن API، نه بانک).</summary>
public interface ISsoClients
{
    /// <summary>آدرس دریافت بلیت در سامانه؛ null یعنی SSO برای این سامانه پیکربندی نشده است.</summary>
    string? LandingUrl(string applicationCode);
}
