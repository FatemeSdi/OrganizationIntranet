namespace OrganizationIntranet.Contracts;

/// <summary>گیرندگان اعلانی که مدیر از پنل می‌فرستد: یک کاربر، یک واحد سازمانی (با همه‌ی زیرواحدهایش) یا همه‌ی کاربران فعال.</summary>
public static class AdminNotificationAudience
{
    public const string User = "User", Unit = "Unit", All = "All";
    public const string NotificationType = "AdminMessage";
    public const int MaxTitleLength = 200, MaxMessageLength = 2000, PageSize = 20;
    public static bool Valid(string? value) => value is User or Unit or All;
}
/// <summary>ارسال اعلان. UserId فقط برای گیرنده‌ی کاربر و UnitId فقط برای گیرنده‌ی واحد لازم است.</summary>
public record AdminNotificationRequest(string? Title, string? Message, string? AudienceType, long? UserId = null, int? UnitId = null);
/// <summary>یک اعلان ارسال‌شده. AudienceLabel نام کاربر، مسیر واحد یا «همه‌ی کاربران» است؛ ReadCount شمار گیرندگانی که خوانده‌اند.</summary>
public record AdminNotificationDto(long NotificationId, string Title, string Message, string AudienceType, string AudienceLabel, int RecipientCount, int ReadCount, bool IsActive, DateTime CreatedAt, string SentBy);
public record AdminNotificationListDto(List<AdminNotificationDto> Items, int Total, int Page, int PageSize)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(Total / (double)Math.Max(1, PageSize)));
}
