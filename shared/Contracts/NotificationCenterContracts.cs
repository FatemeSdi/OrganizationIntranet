namespace OrganizationIntranet.Contracts;

/// <summary>مرکز اعلان Portal: اعلان سامانه‌ها و هشدار درخواست دسترسی (کارتابل نظارت) در یک فهرست صفحه‌بندی‌شده.</summary>
public static class NotificationCenter
{
    public const string SystemKind = "system", RequestKind = "request";
    public const string Unread = "unread", Read = "read";
    public const int PageSize = 20;
    /// <summary>بازه‌های مجاز فیلتر تاریخ (روز گذشته).</summary>
    public static readonly IReadOnlyList<(int Days, string Label)> Periods = [(1, "۲۴ ساعت اخیر"), (7, "۷ روز اخیر"), (30, "۳۰ روز اخیر"), (90, "۳ ماه اخیر")];
    public static bool ValidKind(string? kind) => kind is null or SystemKind or RequestKind;
    public static bool ValidStatus(string? status) => status is null or Unread or Read;
    public static bool ValidDays(int? days) => days is null || Periods.Any(p => p.Days == days);
}
public record NotificationCenterItemDto(string Kind, long Id, string Source, string Title, DateTime CreatedAt, bool Unread, bool HasDestination);
/// <summary>AllCount و UnreadCount برای تب‌ها، با همان فیلتر منبع و تاریخ ولی بدون فیلتر وضعیت؛ Total شمار فیلترشده برای صفحه‌بندی است.</summary>
public record NotificationCenterDto(List<NotificationCenterItemDto> Items, int Total, int Page, int PageSize, int AllCount, int UnreadCount)
{
    public int ReadCount => AllCount - UnreadCount;
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(Total / (double)Math.Max(1, PageSize)));
}

public record SupervisedApplicationDto(int ApplicationId, string Name);
/// <summary>سامانه‌هایی که کاربر ناظر آن‌هاست و شمار درخواست‌ها به تفکیک وضعیت (با فیلتر سامانه‌ی انتخاب‌شده). بدون سامانه یعنی ناظر نیست.</summary>
public record SupervisionSummaryDto(List<SupervisedApplicationDto> Applications, Dictionary<string, int> StatusCounts)
{
    public bool IsSupervisor => Applications.Count > 0;
    public int Count(string? status) => status is null ? StatusCounts.Values.Sum() : StatusCounts.GetValueOrDefault(status);
}
