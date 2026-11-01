namespace OrganizationIntranet.Contracts;

/// <summary>وضعیت‌های درخواست دسترسی و برچسب فارسی هر کدام.</summary>
public static class AccessRequestStatuses
{
    public const string Pending = "Pending", Approved = "Approved", Rejected = "Rejected", Cancelled = "Cancelled";
    public static string Label(string status) => status switch
    { Pending => "در انتظار بررسی", Approved => "تأییدشده", Rejected => "ردشده", Cancelled => "لغوشده", _ => status };
    public static bool IsValid(string status) => status is Pending or Approved or Rejected or Cancelled;
}
/// <summary>سامانه‌ای که کاربر می‌تواند برایش درخواست دسترسی بدهد.</summary>
public record RequestableApplicationDto(int ApplicationId, string Name, string? Description);
/// <summary>ثبت درخواست دسترسی: سامانه و دلیل (حداکثر ۱۰۰۰ نویسه).</summary>
public record CreateAccessRequest(int ApplicationId, string Reason);
/// <summary>تصمیم مدیر: تأیید یا رد، توضیح (برای رد الزامی) و Revision درخواست.</summary>
public record ReviewAccessRequest(bool Approve, string? Note, Guid Revision);
/// <summary>لغو درخواست با Revision فعلی آن.</summary>
public record CancelAccessRequest(Guid Revision);
/// <summary>درخواست دسترسی با اطلاعات درخواست‌دهنده و تصمیم؛ Revision باید در تأیید، رد یا لغو برگردانده شود.</summary>
public record AccessRequestDto(long Id, int ApplicationId, string ApplicationName, long UserId, string Username,
    string DisplayName, string Reason, string Status, DateTime CreatedAt, DateTime? ResolvedAt, string? Reviewer,
    string? ReviewNote, Guid Revision);
/// <summary>صفحه‌ای از درخواست‌ها.</summary>
public record AccessRequestListDto(List<AccessRequestDto> Items, int Total, int Page, int PageSize);
