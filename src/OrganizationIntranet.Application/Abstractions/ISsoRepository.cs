using OrganizationIntranet.Domain.Entities;

namespace OrganizationIntranet.Application.Abstractions;

/// <summary>سامانه‌ای که کاربر الان به آن دسترسی دارد.</summary>
public sealed record SsoTarget(int ApplicationId, string ApplicationCode, long UserId);
/// <summary>نتیجه‌ی مصرف بلیت: کاربر اینترانت و نامی که باید به سامانه داده شود (نگاشت‌شده یا همان نام کاربری اینترانت).</summary>
public sealed record SsoRedeemed(User User, string ApplicationUsername);
/// <summary>یک سامانه‌ی فعال با وضعیت دسترسی کاربر و نام کاربری نگاشت‌شده‌اش (در صورت وجود).</summary>
public sealed record SsoAccountRow(int ApplicationId, string ApplicationCode, string ApplicationName, bool HasAccess, string? ExternalUsername);

public interface ISsoRepository
{
    /// <summary>null یعنی سامانه فعال نیست یا کاربر (فعال) به آن دسترسی ندارد.</summary>
    Task<SsoTarget?> TargetAsync(long userId, int applicationId);
    void AddTicket(SsoTicket ticket);
    /// <summary>
    /// بلیت را به‌صورت اتمیک مصرف می‌کند (فقط یک بار، فقط پیش از انقضا، فقط برای همان سامانه) و اگر کاربر هنوز فعال است و دسترسی دارد
    /// کاربر و نام کاربری او در آن سامانه را برمی‌گرداند. بلیت نامعتبر، کاربری که دسترسی‌اش رفته، یا نامی که متعلق به کاربر دیگری است null می‌دهد.
    /// </summary>
    Task<SsoRedeemed?> RedeemAsync(byte[] ticketHash, string applicationCode, DateTime now);
    Task<User?> FindUserAsync(long userId);
    /// <summary>همه‌ی سامانه‌های فعال با دسترسی فعلی کاربر و نگاشت او.</summary>
    Task<List<SsoAccountRow>> AccountRowsAsync(long userId);
    Task<string?> ActiveApplicationCodeAsync(int applicationId);
    Task<UserApplicationAccount?> FindAccountAsync(long userId, int applicationId);
    /// <summary>آیا این نام در آن سامانه به کاربر دیگری تعلق دارد؟ (نگاشت دیگران، یا نام کاربری اینترانت کاربری که نگاشت ندارد)</summary>
    Task<bool> ApplicationUsernameTakenAsync(int applicationId, string name, long exceptUserId);
    void AddAccount(UserApplicationAccount account);
    void RemoveAccount(UserApplicationAccount account);
    Task DeleteExpiredAsync(DateTime before);
    Task SaveAsync();
}

/// <summary>تنظیمات SSO هر سامانه (در تنظیمات امن API، نه بانک).</summary>
public interface ISsoClients
{
    /// <summary>آدرس دریافت بلیت در سامانه؛ null یعنی SSO برای این سامانه پیکربندی نشده است.</summary>
    string? LandingUrl(string applicationCode);
}
