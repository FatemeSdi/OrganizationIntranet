using Microsoft.EntityFrameworkCore;
using OrganizationIntranet.Application.Abstractions;
using OrganizationIntranet.Contracts;
using OrganizationIntranet.Domain.Entities;

namespace OrganizationIntranet.Api.Data;

public sealed class NotificationIntegrationRepository(AppDbContext db) : INotificationIntegrationRepository
{
    public Task<ApplicationServiceRegistration?> RegistrationAsync(string applicationCode) => db.ApplicationServices.Include(s => s.Application)
        .SingleOrDefaultAsync(s => s.Application.ApplicationCode == applicationCode && s.Application.IsActive && s.IsActive && s.ServiceCode == ApplicationCatalogOptions.NotificationsService);
    public async Task<User?> RecipientAsync(string username, int applicationId)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Username == username && u.IsActive);
        return user is not null && await ApplicationAccessQueries.Available(db, user.UserId).AnyAsync(a => a.ApplicationId == applicationId) ? user : null;
    }
    public Task<Notification?> ExistingAsync(int applicationId, long userId, string externalId) => db.Notifications.Include(n => n.UserNotifications)
        .SingleOrDefaultAsync(n => n.ApplicationId == applicationId && n.ExternalRecipientId == userId && n.ExternalId == externalId);
    public void Add(Notification notification) => db.Notifications.Add(notification);
    public async Task SaveAsync() => await db.SaveChangesAsync();
}
