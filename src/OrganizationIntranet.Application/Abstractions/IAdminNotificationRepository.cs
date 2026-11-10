using OrganizationIntranet.Domain.Entities;

namespace OrganizationIntranet.Application.Abstractions;

/// <summary>یک ردیف فهرست اعلان‌های ارسال‌شده از پنل، پیش از ساخت برچسب گیرنده.</summary>
public sealed record AdminNotificationRow(Notification Notification, string SentBy, string? UserName, string? UnitPath, int ReadCount);

/// <summary>دسترسی به داده‌ی اعلان‌های سازمانی که مدیر از پنل می‌فرستد؛ پیاده‌سازی در لایه‌ی API.</summary>
public interface IAdminNotificationRepository
{
    /// <summary>شناسه‌ی سامانه‌ی اینترانت (کد INTERANET) که فرستنده‌ی این اعلان‌هاست؛ null اگر نبود.</summary>
    Task<int?> HomeApplicationIdAsync();
    Task<User?> FindUserAsync(long userId);
    Task<OrgUnit?> FindUnitAsync(int unitId);
    /// <summary>شناسه‌ی کاربران فعال گیرنده: همه، یک کاربر، یا کاربران واحدی با مسیر داده‌شده و همه‌ی زیرواحدهایش.</summary>
    Task<List<long>> RecipientsAsync(string audienceType, long? userId, string? unitPath);
    void Add(Notification notification);
    /// <summary>اعلان ارسال‌شده از پنل (با گیرنده) با شناسه؛ اعلان سامانه‌های دیگر برنمی‌گردد.</summary>
    Task<Notification?> FindSentAsync(long id);
    Task<(List<AdminNotificationRow> Items, int Total)> PageAsync(int page, int pageSize);
    Task SaveAsync();
}
