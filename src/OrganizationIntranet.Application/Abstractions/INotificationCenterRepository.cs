namespace OrganizationIntranet.Application.Abstractions;

/// <summary>یک ردیف مرکز اعلان پیش از ساخت متن نهایی؛ برای هشدار درخواست، Title خالی و نام درخواست‌کننده پر است.</summary>
public sealed class NotificationCenterRow
{
    public string Kind { get; init; } = "";
    public long Id { get; init; }
    public string Source { get; init; } = "";
    public string? Title { get; init; }
    public string? RequesterName { get; init; }
    public string? RequesterLastName { get; init; }
    public DateTime CreatedAt { get; init; }
    public bool IsRead { get; init; }
    public string? BaseUrl { get; init; }
    public string? TargetUrl { get; init; }
}

/// <param name="Kind">null = هر دو منبع</param>
/// <param name="Read">null = همه، true = خوانده‌شده، false = خوانده‌نشده</param>
public sealed record NotificationCenterQuery(string? Kind, bool? Read, DateTime? Since, int Page, int PageSize);

public interface INotificationCenterRepository
{
    /// <summary>Total با فیلتر وضعیت؛ All و Unread بدون آن (برای تب‌ها). دید هر منبع همان دید فهرست‌های جداگانه است.</summary>
    Task<(List<NotificationCenterRow> Items, int Total, int All, int Unread)> PageAsync(long userId, NotificationCenterQuery query);
}
