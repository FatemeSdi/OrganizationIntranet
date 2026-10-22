using Microsoft.EntityFrameworkCore;
using OrganizationIntranet.Application.Abstractions;
using OrganizationIntranet.Contracts;

namespace OrganizationIntranet.Api.Data;

/// <summary>اعلان سامانه‌ها و هشدار درخواست دسترسی با UNION ALL در بانک صفحه‌بندی می‌شوند؛ دید هر منبع همان کوئری فهرست جداگانه‌ی آن است.</summary>
public sealed class NotificationCenterRepository(AppDbContext db) : INotificationCenterRepository
{
    public async Task<(List<NotificationCenterRow> Items, int Total, int All, int Unread)> PageAsync(long userId, NotificationCenterQuery query)
    {
        IQueryable<NotificationCenterRow>? rows = null;
        if (query.Kind is null or NotificationCenter.SystemKind)
            rows = NotificationQueries.Visible(db, userId).Select(n => new NotificationCenterRow
            {
                Kind = NotificationCenter.SystemKind, Id = n.UserNotificationId, Source = n.Notification.Application.ApplicationName,
                Title = n.Notification.Title, RequesterName = null, RequesterLastName = null, CreatedAt = n.CreatedAt, IsRead = n.IsRead,
                BaseUrl = n.Notification.Application.BaseUrl, TargetUrl = n.Notification.TargetUrl,
            });
        if (query.Kind is null or NotificationCenter.RequestKind)
        {
            var alerts = SupervisionQueries.Alerts(db, userId).Select(a => new NotificationCenterRow
            {
                Kind = NotificationCenter.RequestKind, Id = a.AccessRequestAlertId, Source = a.Request.Application.ApplicationName,
                Title = null, RequesterName = a.Request.User.Name, RequesterLastName = a.Request.User.LastName, CreatedAt = a.CreatedAt, IsRead = a.IsRead,
                BaseUrl = null, TargetUrl = null,
            });
            rows = rows is null ? alerts : rows.Concat(alerts);
        }
        if (query.Since is { } since) rows = rows!.Where(r => r.CreatedAt >= since);

        var all = await rows!.CountAsync();
        var unread = await rows!.CountAsync(r => !r.IsRead);
        var filtered = query.Read is { } read ? rows!.Where(r => r.IsRead == read) : rows!;
        var total = query.Read switch { true => all - unread, false => unread, null => all };
        var items = await filtered.OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).AsNoTracking().ToListAsync();
        return (items, total, all, unread);
    }
}
