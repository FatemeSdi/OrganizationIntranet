using OrganizationIntranet.Contracts;
using OrganizationIntranet.Domain.Entities;

namespace OrganizationIntranet.Api.Data;

internal static class NotificationQueries
{
    // اعلان فقط از سامانه‌ای دیده می‌شود که کاربر الان به آن دسترسی دارد و سرویس فعال NOTIFICATIONS دارد.
    internal static IQueryable<UserNotification> Visible(AppDbContext db, long userId)
    {
        var ids = ApplicationAccessQueries.Available(db, userId)
            .Where(a => a.Services.Any(s => s.IsActive && s.ServiceCode == ApplicationCatalogOptions.NotificationsService))
            .Select(a => a.ApplicationId);
        return db.UserNotifications.Where(n => n.UserId == userId && ids.Contains(n.Notification.ApplicationId));
    }
}
