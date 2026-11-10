namespace OrganizationIntranet.Domain.Entities;

/// <summary>اعلانی که یک سامانه فرستاده است. اعلان واردشده از سامانه‌ی بیرونی با (ApplicationId، ExternalRecipientId، ExternalId) یکتاست.</summary>
public class Notification
{
    public long NotificationId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? NotificationType { get; set; }
    public long? CreatedBy { get; set; }
    public int ApplicationId { get; set; }
    public string? TargetUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? ExternalId { get; set; }
    public long? ExternalRecipientId { get; set; }
    /// <summary>زمان آخرین به‌روزرسانی اعلان در سامانه‌ی مبدأ؛ فقط نسخه‌ی تازه‌تر اعلان را عوض می‌کند.</summary>
    public DateTime? SourceUpdatedAt { get; set; }
    /// <summary>اعلان فعال برای گیرندگان دیده می‌شود. مدیر می‌تواند اعلان ارسالی خودش را پس بگیرد (غیرفعال) و برگرداند؛ وضعیت خوانده‌شدن حفظ می‌شود.</summary>
    public bool IsActive { get; set; } = true;
    /// <summary>برای اعلانی که مدیر از پنل فرستاده: User، Unit یا All. برای اعلان سامانه‌های دیگر خالی است.</summary>
    public string? AudienceType { get; set; }
    public long? AudienceUserId { get; set; }
    public int? AudienceUnitId { get; set; }
    /// <summary>شمار گیرندگان در لحظه‌ی ارسال (فقط اعلان ارسالی مدیر).</summary>
    public int? RecipientCount { get; set; }
    public DateTime? ChangedAt { get; set; }

    public User? CreatedByUser { get; set; }
    public Application Application { get; set; } = null!;
    public ICollection<UserNotification> UserNotifications { get; set; } = new List<UserNotification>();
}
