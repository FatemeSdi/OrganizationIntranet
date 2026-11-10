using OrganizationIntranet.Contracts;
using OrganizationIntranet.Domain.Entities;

namespace OrganizationIntranet.Api.Data;

/// <summary>کوئری مشترک اعلان‌های قابل‌دیدن کاربر.</summary>
internal static class NotificationQueries
{
    /// <summary>
    /// اعلان‌های کاربر. اعلان سامانه‌های دیگر فقط از سامانه‌ای دیده می‌شود که کاربر الان به آن دسترسی دارد و سرویس فعال NOTIFICATIONS دارد.
    /// اعلان سازمانیِ ارسالی مدیر (AudienceType پر) به سامانه‌ای وابسته نیست و فقط به کاربر فعال می‌رسد. اعلان پس‌گرفته‌شده (IsActive = false) از هر دو نوع دیده نمی‌شود.
    /// </summary>
    internal static IQueryable<UserNotification> Visible(AppDbContext db, long userId)
    {
        var ids = ApplicationAccessQueries.Available(db, userId)
            .Where(a => a.Services.Any(s => s.IsActive && s.ServiceCode == ApplicationCatalogOptions.NotificationsService))
            .Select(a => a.ApplicationId);
        return db.UserNotifications.Where(n => n.UserId == userId && n.Notification.IsActive
            && (n.Notification.AudienceType != null ? db.Users.Any(u => u.UserId == userId && u.IsActive) : ids.Contains(n.Notification.ApplicationId)));
    }
}
