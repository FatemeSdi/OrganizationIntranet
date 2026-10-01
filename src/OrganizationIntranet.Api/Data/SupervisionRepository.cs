using Microsoft.EntityFrameworkCore;
using OrganizationIntranet.Application.Abstractions;
using OrganizationIntranet.Application.Services;
using OrganizationIntranet.Contracts;
using OrganizationIntranet.Domain.Entities;

namespace OrganizationIntranet.Api.Data;

public sealed class SupervisionRepository(AppDbContext db) : ISupervisionRepository
{
    public Task<List<ApplicationSupervisionGroup>> GroupsAsync(int applicationId) => db.ApplicationSupervisionGroups.Include(g => g.Members)
        .Where(g => g.ApplicationId == applicationId).OrderBy(g => g.Name).ToListAsync();
    public Task<ApplicationSupervisionGroup?> GroupAsync(int applicationId, int id) => db.ApplicationSupervisionGroups.Include(g => g.Members)
        .SingleOrDefaultAsync(g => g.ApplicationId == applicationId && g.ApplicationSupervisionGroupId == id);
    public async Task<bool> ValidPrincipalsAsync(IReadOnlyCollection<long> users, IReadOnlyCollection<int> roles) =>
        await db.Users.CountAsync(u => users.Contains(u.UserId)) == users.Count && await db.Roles.CountAsync(r => roles.Contains(r.RoleId)) == roles.Count;
    public void AddGroup(ApplicationSupervisionGroup group) => db.ApplicationSupervisionGroups.Add(group);
    public void SetMembers(ApplicationSupervisionGroup group, IReadOnlyCollection<long> users, IReadOnlyCollection<int> roles)
    {
        db.ApplicationSupervisors.RemoveRange(group.Members.Where(m => m.UserId.HasValue ? !users.Contains(m.UserId.Value) : !roles.Contains(m.RoleId!.Value)).ToList());
        foreach (var user in users.Where(id => !group.Members.Any(m => m.UserId == id))) group.Members.Add(new() { UserId = user });
        foreach (var role in roles.Where(id => !group.Members.Any(m => m.RoleId == id))) group.Members.Add(new() { RoleId = role });
    }
    public async Task EnqueueAsync(ApplicationAccessRequest request)
    {
        var app = await db.Applications.SingleAsync(a => a.ApplicationId == request.ApplicationId);
        var users = await SupervisionQueries.Recipients(db, request.ApplicationId).Select(u => u.UserId).ToListAsync();
        foreach (var user in users)
            db.AccessRequestAlerts.Add(new() { Request = request, UserId = user, CreatedAt = DateTime.UtcNow,
                InApp = app.AccessRequestNotificationChannels is "InApp" or "Both",
                SmsStatus = app.AccessRequestNotificationChannels is "Sms" or "Both" ? "Pending" : "NotRequested",
                NextAttemptAt = app.AccessRequestNotificationChannels is "Sms" or "Both" ? DateTime.UtcNow : null });
    }
    private IQueryable<AccessRequestAlert> Visible(long userId)
    {
        var apps = SupervisionQueries.Applications(db, userId);
        return db.AccessRequestAlerts.Where(a => a.UserId == userId && a.InApp && apps.Contains(a.Request.ApplicationId));
    }
    public async Task<(List<AccessRequestAlert> Items, int UnreadCount)> AlertsAsync(long userId)
    {
        var query = Visible(userId);
        var count = await query.CountAsync(a => !a.IsRead);
        return (await query.AsNoTracking().Include(a => a.Request).ThenInclude(r => r.Application).Include(a => a.Request).ThenInclude(r => r.User)
            .OrderBy(a => a.IsRead).ThenByDescending(a => a.AccessRequestAlertId).Take(50).ToListAsync(), count);
    }
    public async Task MarkReadAsync(long userId, long? alertId)
    {
        var alerts = await Visible(userId).Where(a => !alertId.HasValue || a.AccessRequestAlertId == alertId.Value).ToListAsync();
        if (alertId.HasValue && alerts.Count == 0) throw new KeyNotFoundException();
        foreach (var alert in alerts.Where(a => !a.IsRead)) { alert.IsRead = true; alert.ReadAt = DateTime.UtcNow; alert.Revision = Guid.NewGuid(); }
    }
    public async Task<(List<ApplicationAccessRequest> Items, int Total)> InboxAsync(long userId, int page, int pageSize)
    {
        var apps = SupervisionQueries.Applications(db, userId);
        var query = db.ApplicationAccessRequests.AsNoTracking().Where(r => apps.Contains(r.ApplicationId));
        var count = await query.CountAsync();
        return (await query.Include(r => r.User).Include(r => r.Application).OrderBy(r => r.Status != "Pending").ThenByDescending(r => r.ApplicationAccessRequestId)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(), count);
    }
    public async Task<(List<AccessRequestAlert> Items, int Total)> SmsAsync(int page, int pageSize)
    {
        var query = db.AccessRequestAlerts.AsNoTracking().Where(a => a.SmsStatus != "NotRequested");
        var count = await query.CountAsync();
        return (await query.Include(a => a.User).Include(a => a.Request).ThenInclude(r => r.Application).OrderByDescending(a => a.AccessRequestAlertId)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(), count);
    }
    public Task<AccessRequestAlert?> FindSmsAsync(long id) => db.AccessRequestAlerts.Include(a => a.Request).SingleOrDefaultAsync(a => a.AccessRequestAlertId == id);
    public async Task SaveAsync()
    {
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateConcurrencyException) { throw new AccessRequestConflictException(); }
    }
}
