using OrganizationIntranet.Domain.Entities;

namespace OrganizationIntranet.Application.Abstractions;

/// <summary>دسترسی به داده‌ی نظارت: گروه‌ها، هشدارها، کارتابل و صف پیامک؛ پیاده‌سازی در لایه‌ی API.</summary>
public interface ISupervisionRepository
{
    /// <summary>گروه‌های نظارت یک سامانه با اعضا.</summary>
    Task<List<ApplicationSupervisionGroup>> GroupsAsync(int applicationId);
    /// <summary>یک گروه از همان سامانه.</summary>
    Task<ApplicationSupervisionGroup?> GroupAsync(int applicationId, int id);
    /// <summary>آیا همه‌ی کاربران و نقش‌های اعضا موجود و فعال‌اند.</summary>
    Task<bool> ValidPrincipalsAsync(IReadOnlyCollection<long> users, IReadOnlyCollection<int> roles);
    /// <summary>گروه تازه را اضافه می‌کند.</summary>
    void AddGroup(ApplicationSupervisionGroup group);
    /// <summary>اعضای گروه را با مجموعه‌ی داده‌شده یکی می‌کند.</summary>
    void SetMembers(ApplicationSupervisionGroup group, IReadOnlyCollection<long> users, IReadOnlyCollection<int> roles);
    /// <summary>برای درخواست تازه، هشدار ناظران و صف پیامک را می‌سازد.</summary>
    Task EnqueueAsync(ApplicationAccessRequest request);
    /// <summary>هشدارهای کاربر و شمار خوانده‌نشده‌ها.</summary>
    Task<(List<AccessRequestAlert> Items, int UnreadCount)> AlertsAsync(long userId);
    /// <summary>یک هشدار یا (alertId خالی) همه‌ی هشدارهای کاربر را خوانده‌شده می‌کند.</summary>
    Task MarkReadAsync(long userId, long? alertId);
    /// <summary>درخواست‌های سامانه‌های تحت نظارت کاربر با فیلتر وضعیت و سامانه.</summary>
    Task<(List<ApplicationAccessRequest> Items, int Total)> InboxAsync(long userId, int page, int pageSize, string? status = null, int? applicationId = null);
    /// <summary>سامانه‌های تحت نظارت کاربر و شمار درخواست‌هایشان به تفکیک وضعیت.</summary>
    Task<(List<(int Id, string Name)> Applications, Dictionary<string, int> StatusCounts)> InboxSummaryAsync(long userId, int? applicationId);
    /// <summary>صف و سابقه‌ی پیامک‌ها (صفحه‌بندی).</summary>
    Task<(List<AccessRequestAlert> Items, int Total)> SmsAsync(int page, int pageSize);
    /// <summary>یک پیامک برای ارسال دوباره.</summary>
    Task<AccessRequestAlert?> FindSmsAsync(long id);
    /// <summary>تغییرات را ذخیره می‌کند.</summary>
    Task SaveAsync();
}
