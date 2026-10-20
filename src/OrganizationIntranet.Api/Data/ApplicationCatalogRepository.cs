using Microsoft.EntityFrameworkCore;
using OrganizationIntranet.Application.Abstractions;
using OrganizationIntranet.Contracts;
using OrganizationIntranet.Domain.Entities;
using AppEntity = OrganizationIntranet.Domain.Entities.Application;

namespace OrganizationIntranet.Api.Data;

public sealed class ApplicationCatalogRepository(AppDbContext db) : IApplicationCatalogRepository
{
    // Query live assignments, not the role claims cached at login. Public means no application grant is required.
    private IQueryable<AppEntity> Available(long userId) => ApplicationAccessQueries.Available(db, userId);

    public Task<List<AppEntity>> ListAsync() => db.Applications.OrderBy(a => a.DisplayOrder).ThenBy(a => a.ApplicationId).ToListAsync();
    public Task<AppEntity?> FindAsync(int id) => db.Applications.Include(a => a.AccessGrants).Include(a => a.Services)
        .AsSplitQuery().FirstOrDefaultAsync(a => a.ApplicationId == id);
    public Task<List<AppEntity>> AvailableAsync(long userId) => Available(userId).AsNoTracking()
        .Include(a => a.Services.Where(s => s.IsActive)).AsSplitQuery()
        .OrderBy(a => a.DisplayOrder).ThenBy(a => a.ApplicationId).ToListAsync();
    private IQueryable<UserNotification> VisibleNotifications(long userId)
    {
        var ids = Available(userId).Where(a => a.Services.Any(s => s.IsActive && s.ServiceCode == ApplicationCatalogOptions.NotificationsService))
            .Select(a => a.ApplicationId);
        return db.UserNotifications.Where(n => n.UserId == userId && ids.Contains(n.Notification.ApplicationId));
    }
    public Task<UserNotification?> FindNotificationAsync(long userId, long id) => VisibleNotifications(userId)
        .Include(n => n.Notification).ThenInclude(n => n.Application).SingleOrDefaultAsync(n => n.UserNotificationId == id);
    public Task<Dictionary<int, int>> UnreadCountsAsync(long userId) => VisibleNotifications(userId).Where(n => !n.IsRead)
        .GroupBy(n => n.Notification.ApplicationId).Select(g => new { ApplicationId = g.Key, Count = g.Count() })
        .ToDictionaryAsync(g => g.ApplicationId, g => g.Count);
    public Task<List<UserNotification>> NotificationsAsync(long userId, bool unreadOnly, int? limit = null)
    {
        var query = VisibleNotifications(userId).Include(n => n.Notification).ThenInclude(n => n.Application)
            .Where(n => !unreadOnly || !n.IsRead).OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.UserNotificationId).AsQueryable();
        if (limit.HasValue) query = query.Take(limit.Value).AsNoTracking();
        return query.ToListAsync();
    }
    public async Task<bool> PrincipalsExistAsync(IReadOnlyCollection<long> userIds, IReadOnlyCollection<int> roleIds) =>
        await db.Users.CountAsync(u => userIds.Contains(u.UserId)) == userIds.Count
        && await db.Roles.CountAsync(r => roleIds.Contains(r.RoleId)) == roleIds.Count;
    public void Add(AppEntity application) => db.Applications.Add(application);
    public void SetAccess(AppEntity application, IReadOnlyCollection<long> userIds, IReadOnlyCollection<int> roleIds)
    {
        db.ApplicationAccessGrants.RemoveRange(application.AccessGrants.Where(g =>
            g.UserId.HasValue ? !userIds.Contains(g.UserId.Value) : !roleIds.Contains(g.RoleId!.Value)).ToList());
        foreach (var id in userIds.Where(id => !application.AccessGrants.Any(g => g.UserId == id)))
            application.AccessGrants.Add(new ApplicationAccess { UserId = id, CreatedAt = DateTime.UtcNow });
        foreach (var id in roleIds.Where(id => !application.AccessGrants.Any(g => g.RoleId == id)))
            application.AccessGrants.Add(new ApplicationAccess { RoleId = id, CreatedAt = DateTime.UtcNow });
    }
    public void SetServices(AppEntity application, IReadOnlyCollection<ApplicationServiceRegistration> services)
    {
        db.ApplicationServices.RemoveRange(application.Services.Where(s => !services.Any(n => n.ServiceCode == s.ServiceCode)).ToList());
        foreach (var incoming in services)
        {
            var existing = application.Services.FirstOrDefault(s => s.ServiceCode == incoming.ServiceCode);
            if (existing is null) application.Services.Add(incoming);
            else
            {
                if (existing.EndpointUrl != incoming.EndpointUrl) { existing.SyncCursor = null; existing.LastSyncedAt = null; existing.LastSyncError = null; }
                existing.DisplayName = incoming.DisplayName; existing.EndpointUrl = incoming.EndpointUrl; existing.IsActive = incoming.IsActive;
            }
        }
    }
    public async Task SaveAsync() => await db.SaveChangesAsync();
}
