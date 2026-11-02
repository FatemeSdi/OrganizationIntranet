namespace OrganizationIntranet.Contracts;

/// <summary>روش‌های اطلاع‌رسانی درخواست دسترسی: InApp، Sms، Both.</summary>
public static class AccessRequestChannels
{
    public static bool Valid(string value) => value is "InApp" or "Sms" or "Both";
    public static string Label(string value) => value switch { "InApp" => "درون‌برنامه‌ای", "Sms" => "پیامک موبایل", "Both" => "درون‌برنامه‌ای و پیامک", _ => value };
}
/// <summary>گروه نظارت با شناسه‌ی کاربران و نقش‌های عضو و Revision برای کنترل هم‌زمانی.</summary>
public record SupervisionGroupDto(int Id, int ApplicationId, string Name, bool IsActive, List<long> UserIds, List<int> RoleIds, Guid Revision);
/// <summary>ساخت (Id=0) یا ویرایش گروه نظارت؛ گروه فعال حداقل یک عضو می‌خواهد.</summary>
public record SupervisionGroupRequest(int Id, string Name, bool IsActive, List<long> UserIds, List<int> RoleIds, Guid Revision);
/// <summary>هشدار «درخواست دسترسی جدید» برای ناظر.</summary>
public record AccessRequestAlertDto(long Id, long RequestId, string ApplicationName, string Requester, DateTime CreatedAt, bool IsRead);
/// <summary>هشدارهای یک ناظر و شمار خوانده‌نشده‌ها.</summary>
public record AccessRequestAlertsDto(List<AccessRequestAlertDto> Items, int UnreadCount);
/// <summary>یک پیامک صف اطلاع‌رسانی: گیرنده، وضعیت، شمار تلاش و خطا.</summary>
public record AccessRequestSmsDto(long Id, long RequestId, string ApplicationName, string Recipient, string Status, int Attempts, string? Error, DateTime? SentAt);
/// <summary>صفحه‌ای از صف پیامک‌ها.</summary>
public record AccessRequestSmsListDto(List<AccessRequestSmsDto> Items, int Total, int Page, int PageSize);
