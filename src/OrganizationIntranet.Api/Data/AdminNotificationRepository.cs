using Microsoft.EntityFrameworkCore;
using OrganizationIntranet.Application.Abstractions;
using OrganizationIntranet.Contracts;
using OrganizationIntranet.Domain.Entities;

namespace OrganizationIntranet.Api.Data;

/// <summary>پیاده‌سازی EF Core داده‌ی اعلان‌های سازمانی ارسالی مدیر (ردیف Notification با گیرنده + ردیف تحویل برای هر کاربر).</summary>
public sealed class AdminNotificationRepository(AppDbContext db) : IAdminNotificationRepository
{
    public async Task<int?> HomeApplicationIdAsync() =>
        await db.Applications.Where(a => a.ApplicationCode == AdminPermissions.ApplicationCode).Select(a => (int?)a.ApplicationId).SingleOrDefaultAsync();
    public Task<User?> FindUserAsync(long userId) => db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.UserId == userId);
    public Task<OrgUnit?> FindUnitAsync(int unitId) => db.OrgUnits.AsNoTracking().SingleOrDefaultAsync(u => u.UnitId == unitId);
    /// <summary>کاربران فعال گیرنده؛ زیرواحد با پیشوند مسیر کامل (مسیر + «/») شناخته می‌شود، مثل دسترسی سامانه بر اساس چارت.</summary>
    public Task<List<long>> RecipientsAsync(string audienceType, long? userId, string? unitPath)
    {
        var active = db.Users.AsNoTracking().Where(u => u.IsActive);
        return audienceType switch
        {
            AdminNotificationAudience.User => active.Where(u => u.UserId == userId).Select(u => u.UserId).ToListAsync(),
            AdminNotificationAudience.Unit => active.Where(u => u.Unit != null && (u.Unit.FullPath == unitPath || u.Unit.FullPath.StartsWith(unitPath! + "/"))).Select(u => u.UserId).ToListAsync(),
            _ => active.Select(u => u.UserId).ToListAsync(),
        };
    }
    public void Add(Notification notification) => db.Notifications.Add(notification);
    public Task<Notification?> FindSentAsync(long id) => db.Notifications.SingleOrDefaultAsync(n => n.NotificationId == id && n.AudienceType != null);
    public async Task<(List<AdminNotificationRow> Items, int Total)> PageAsync(int page, int pageSize)
    {
        var query = db.Notifications.AsNoTracking().Where(n => n.AudienceType != null);
        var total = await query.CountAsync();
        var rows = await query.OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.NotificationId).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(n => new
            {
                Notification = n,
                SentBy = n.CreatedByUser == null ? "" : n.CreatedByUser.Name + " " + n.CreatedByUser.LastName,
                UserName = db.Users.Where(u => u.UserId == n.AudienceUserId).Select(u => u.Name + " " + u.LastName).FirstOrDefault(),
                UnitPath = db.OrgUnits.Where(u => u.UnitId == n.AudienceUnitId).Select(u => u.FullPath).FirstOrDefault(),
                Read = n.UserNotifications.Count(x => x.IsRead),
            }).ToListAsync();
        return (rows.Select(r => new AdminNotificationRow(r.Notification, r.SentBy.Trim(), r.UserName?.Trim(), r.UnitPath, r.Read)).ToList(), total);
    }
    public Task SaveAsync() => db.SaveChangesAsync();
}
