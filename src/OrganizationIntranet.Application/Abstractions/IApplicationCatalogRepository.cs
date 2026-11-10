using OrganizationIntranet.Domain.Entities;
using AppEntity = OrganizationIntranet.Domain.Entities.Application;

namespace OrganizationIntranet.Application.Abstractions;

/// <summary>دسترسی به داده‌ی کاتالوگ سامانه‌ها و اعلان‌های کاربر؛ پیاده‌سازی در لایه‌ی API.</summary>
public interface IApplicationCatalogRepository
{
    /// <summary>همه‌ی سامانه‌ها به ترتیب نمایش.</summary>
    Task<List<AppEntity>> ListAsync();
    /// <summary>یک سامانه با دسترسی‌ها و سرویس‌ها (برای ویرایش).</summary>
    Task<AppEntity?> FindAsync(int id);
    /// <summary>سامانه‌های فعالِ در اختیار کاربر (مستقیم، از راه نقش یا عمومی) برای پورتال.</summary>
    Task<List<AppEntity>> AvailableAsync(long userId);
    /// <summary>سامانه‌های فعال و عمومی برای بازدیدکننده‌ی ناشناس؛ هیچ داده‌ی کاربر یا انتساب دسترسی را نمی‌خواند.</summary>
    Task<List<AppEntity>> PublicAsync();
    /// <summary>اعلان‌های کاربر (فقط خوانده‌نشده‌ها با unreadOnly، محدود به limit).</summary>
    Task<List<UserNotification>> NotificationsAsync(long userId, bool unreadOnly, int? limit = null);
    /// <summary>شمار اعلان خوانده‌نشده‌ی کاربر به تفکیک سامانه.</summary>
    Task<Dictionary<int, int>> UnreadCountsAsync(long userId);
    /// <summary>یک اعلان فقط اگر مال همین کاربر و در دید او باشد.</summary>
    Task<UserNotification?> FindNotificationAsync(long userId, long id);
    /// <summary>آیا همه‌ی کاربران و نقش‌های داده‌شده وجود دارند.</summary>
    Task<bool> PrincipalsExistAsync(IReadOnlyCollection<long> userIds, IReadOnlyCollection<int> roleIds);
    /// <summary>سامانه‌ی تازه را اضافه می‌کند.</summary>
    void Add(AppEntity application);
    /// <summary>دسترسی سامانه را با مجموعه‌ی کاربران و نقش‌های داده‌شده یکی می‌کند.</summary>
    void SetAccess(AppEntity application, IReadOnlyCollection<long> userIds, IReadOnlyCollection<int> roleIds);
    /// <summary>آیا همه‌ی واحدهای داده‌شده وجود دارند و فعال‌اند؟</summary>
    Task<bool> ActiveUnitsExistAsync(IReadOnlyCollection<int> unitIds);
    /// <summary>واحدهای دارای دسترسی (حالت «محدود به واحد») را با مجموعه‌ی داده‌شده یکی می‌کند.</summary>
    void SetUnitAccess(AppEntity application, IReadOnlyCollection<int> unitIds);
    /// <summary>سرویس‌های سامانه را با فهرست داده‌شده یکی می‌کند.</summary>
    void SetServices(AppEntity application, IReadOnlyCollection<ApplicationServiceRegistration> services);
    /// <summary>تغییرات را در یک تراکنش ذخیره می‌کند.</summary>
    Task SaveAsync();
}
