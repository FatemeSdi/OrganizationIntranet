using OrganizationIntranet.Application.Abstractions;
using OrganizationIntranet.Contracts;

namespace OrganizationIntranet.Application.Services;

/// <summary>مرکز اعلان‌های کاربر: اعلان سامانه‌ها و هشدار درخواست‌های دسترسی در یک فهرست با فیلتر و صفحه‌بندی سمت سرور.</summary>
public sealed class NotificationCenterService(INotificationCenterRepository repository)
{
    /// <summary>یک صفحه از مرکز اعلان. kind=system|request، status=unread|read و days=1|7|30|90 را اعتبارسنجی می‌کند. شمار کل، همه و خوانده‌نشده برای تب‌ها هم برمی‌گردد.</summary>
    public async Task<NotificationCenterDto> PageAsync(long userId, string? kind, string? status, int? days, int page)
    {
        if (!NotificationCenter.ValidKind(kind) || !NotificationCenter.ValidStatus(status) || !NotificationCenter.ValidDays(days) || page is < 1 or > 100000)
            throw new ArgumentException("فیلتر اعلان‌ها معتبر نیست");
        var query = new NotificationCenterQuery(kind, status switch { NotificationCenter.Read => true, NotificationCenter.Unread => false, _ => null },
            days is { } d ? DateTime.UtcNow.AddDays(-d) : null, page, NotificationCenter.PageSize);
        var result = await repository.PageAsync(userId, query);
        return new(result.Items.Select(r => r.Kind == NotificationCenter.RequestKind
                ? new NotificationCenterItemDto(r.Kind, r.Id, r.Source, $"درخواست دسترسی جدید از {r.RequesterName} {r.RequesterLastName}".TrimEnd(), r.CreatedAt, !r.IsRead, true)
                : new NotificationCenterItemDto(r.Kind, r.Id, r.Source, r.Title ?? "", r.CreatedAt, !r.IsRead, ApplicationLinkPolicy.Destination(r.BaseUrl, r.TargetUrl) is not null)).ToList(),
            result.Total, page, NotificationCenter.PageSize, result.All, result.Unread);
    }
}
