using OrganizationIntranet.Application.Abstractions;
using OrganizationIntranet.Contracts;
using OrganizationIntranet.Domain.Entities;

namespace OrganizationIntranet.Application.Services;

/// <summary>
/// اعلان سازمانی از پنل مدیریت: ارسال به یک کاربر، یک واحد (با زیرواحدها) یا همه، فهرست ارسال‌ها و پس‌گرفتن/بازگرداندن.
/// برای هر گیرنده یک ردیف تحویل ساخته می‌شود، پس خوانده‌شدن هر نفر جدا نگه داشته می‌شود و زنگ و مرکز اعلان بدون سازوکار دوم کار می‌کنند.
/// گیرندگان در لحظه‌ی ارسال تعیین می‌شوند؛ کسی که بعداً به واحد می‌پیوندد آن اعلان را نمی‌گیرد. اعلان غیرفعال از فهرست همه‌ی گیرندگان محو می‌شود
/// و با فعال‌کردن دوباره، با همان وضعیت خوانده‌شدن برمی‌گردد.
/// </summary>
public sealed class AdminNotificationService(IAdminNotificationRepository repository)
{
    private static string Required(string? value, int max, string label)
    {
        value = value?.Trim();
        if (string.IsNullOrEmpty(value) || value.Length > max) throw new ArgumentException($"{label} الزامی است و حداکثر {max} کاراکتر دارد");
        return value;
    }

    /// <summary>اعلان را برای گیرندگان فعال می‌سازد و شمار گیرندگان را برمی‌گرداند. گیرنده‌ی خالی، کاربر غیرفعال یا واحد ناموجود خطاست.</summary>
    public async Task<int> SendAsync(AdminNotificationRequest request, long actor)
    {
        if (actor <= 0) throw new ArgumentException("فرستنده معتبر نیست");
        var title = Required(request.Title, AdminNotificationAudience.MaxTitleLength, "عنوان");
        var message = Required(request.Message, AdminNotificationAudience.MaxMessageLength, "متن اعلان");
        if (!AdminNotificationAudience.Valid(request.AudienceType)) throw new ArgumentException("گیرنده‌ی اعلان معتبر نیست");
        long? userId = null; int? unitId = null; string? path = null;
        switch (request.AudienceType)
        {
            case AdminNotificationAudience.User:
                var user = request.UserId is { } id ? await repository.FindUserAsync(id) : null;
                if (user is not { IsActive: true }) throw new ArgumentException("کاربر انتخاب‌شده وجود ندارد یا غیرفعال است");
                userId = user.UserId; break;
            case AdminNotificationAudience.Unit:
                var unit = request.UnitId is { } uid ? await repository.FindUnitAsync(uid) : null;
                if (unit is not { IsActive: true }) throw new ArgumentException("واحد انتخاب‌شده وجود ندارد یا غیرفعال است");
                unitId = unit.UnitId; path = unit.FullPath; break;
        }
        var recipients = (await repository.RecipientsAsync(request.AudienceType!, userId, path)).Distinct().ToList();
        if (recipients.Count == 0) throw new ArgumentException("هیچ کاربر فعالی در این گیرنده نیست");
        var app = await repository.HomeApplicationIdAsync() ?? throw new InvalidOperationException("سامانه‌ی اینترانت (INTERANET) در بانک نیست");
        var now = DateTime.UtcNow;
        var notification = new Notification
        {
            Title = title, Message = message, NotificationType = AdminNotificationAudience.NotificationType, ApplicationId = app, CreatedBy = actor, CreatedAt = now,
            IsActive = true, AudienceType = request.AudienceType, AudienceUserId = userId, AudienceUnitId = unitId, RecipientCount = recipients.Count,
        };
        foreach (var recipient in recipients) notification.UserNotifications.Add(new UserNotification { UserId = recipient, IsRead = false, CreatedAt = now });
        repository.Add(notification);
        await repository.SaveAsync();
        return recipients.Count;
    }

    /// <summary>یک صفحه از اعلان‌های ارسال‌شده، از جدیدترین، با شمار گیرنده و خوانده‌شده.</summary>
    public async Task<AdminNotificationListDto> ListAsync(int page)
    {
        if (page is < 1 or > 100000) throw new ArgumentException("شماره‌ی صفحه معتبر نیست");
        var (items, total) = await repository.PageAsync(page, AdminNotificationAudience.PageSize);
        return new(items.Select(r => new AdminNotificationDto(r.Notification.NotificationId, r.Notification.Title, r.Notification.Message, r.Notification.AudienceType!,
            r.Notification.AudienceType switch { AdminNotificationAudience.User => r.UserName ?? "", AdminNotificationAudience.Unit => r.UnitPath ?? "", _ => "همه‌ی کاربران" },
            r.Notification.RecipientCount ?? 0, r.ReadCount, r.Notification.IsActive, r.Notification.CreatedAt, r.SentBy)).ToList(), total, page, AdminNotificationAudience.PageSize);
    }

    /// <summary>پس‌گرفتن (غیرفعال) یا بازگرداندن (فعال) یک اعلان ارسال‌شده؛ تکرار هر کدام بی‌اثر است. اعلان سامانه‌های دیگر پیدا نمی‌شود.</summary>
    public async Task<bool> SetActiveAsync(long id, bool active)
    {
        var notification = await repository.FindSentAsync(id) ?? throw new KeyNotFoundException();
        if (notification.IsActive != active) { notification.IsActive = active; notification.ChangedAt = DateTime.UtcNow; await repository.SaveAsync(); }
        return notification.IsActive;
    }
}
