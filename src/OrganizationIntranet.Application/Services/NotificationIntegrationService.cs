using OrganizationIntranet.Application.Abstractions;
using OrganizationIntranet.Contracts;
using OrganizationIntranet.Domain.Entities;

namespace OrganizationIntranet.Application.Services;

public sealed class NotificationIntegrationService(INotificationIntegrationRepository repository)
{
    public async Task<NotificationImportResult> ImportAsync(string applicationCode, ApplicationNotificationBatch batch, bool advanceCursor = false)
    {
        if (batch.Items is null || batch.Items.Count > 100 || batch.NextCursor?.Length > 1000) throw new ArgumentException("بسته اعلان معتبر نیست؛ حداکثر ۱۰۰ اعلان مجاز است");
        var registration = await repository.RegistrationAsync(applicationCode) ?? throw new KeyNotFoundException();
        var app = registration.Application;
        var imported = 0; var ignored = 0;
        var keys = new HashSet<(long, string)>();
        foreach (var item in batch.Items)
        {
            if (item is null) throw new ArgumentException("اعلان معتبر نیست");
            var externalId = Required(item.ExternalId, 100); var username = Required(item.Username, 100);
            var title = Required(item.Title, 200); var message = Required(item.Message, 10000);
            if (item.CreatedAt < DateTimeOffset.UnixEpoch || item.UpdatedAt < item.CreatedAt || item.UpdatedAt > DateTimeOffset.UtcNow.AddMinutes(5))
                throw new ArgumentException("زمان اعلان معتبر نیست");
            var target = ApplicationLinkPolicy.Destination(app.BaseUrl, Required(item.TargetUrl, 500))
                ?? throw new ArgumentException("مقصد اعلان باید در همان مبدأ سامانه ثبت‌شده باشد");
            var user = await repository.RecipientAsync(username, app.ApplicationId)
                ?? throw new ArgumentException("گیرنده اعلان معتبر یا مجاز نیست");
            if (!keys.Add((user.UserId, externalId))) throw new ArgumentException("شناسه اعلان در بسته تکراری است");
            var notification = await repository.ExistingAsync(app.ApplicationId, user.UserId, externalId);
            if (notification is not null && notification.SourceUpdatedAt >= item.UpdatedAt.UtcDateTime) { ignored++; continue; }
            if (notification is null)
            {
                notification = new Notification { ApplicationId = app.ApplicationId, ExternalRecipientId = user.UserId, ExternalId = externalId,
                    CreatedAt = item.CreatedAt.UtcDateTime, NotificationType = "External" };
                notification.UserNotifications.Add(new() { UserId = user.UserId, CreatedAt = item.CreatedAt.UtcDateTime });
                repository.Add(notification);
            }
            notification.Title = title; notification.Message = message; notification.TargetUrl = target;
            notification.SourceUpdatedAt = item.UpdatedAt.UtcDateTime;
            var delivery = notification.UserNotifications.Single(n => n.UserId == user.UserId);
            if (item.IsRead && !delivery.IsRead) { delivery.IsRead = true; delivery.ReadAt = DateTime.UtcNow; }
            // An upstream unread snapshot must not undo a user's local read action.
            imported++;
        }
        if (advanceCursor)
        {
            if (batch.NextCursor is not null) registration.SyncCursor = batch.NextCursor;
            registration.LastSyncedAt = DateTime.UtcNow; registration.LastSyncError = null;
        }
        await repository.SaveAsync();
        return new(imported, ignored);
    }
    private static string Required(string? value, int max)
    {
        value = value?.Trim();
        if (string.IsNullOrEmpty(value) || value.Length > max) throw new ArgumentException("فیلدهای اعلان ناقص یا بیش از حد مجاز است");
        return value;
    }
}
