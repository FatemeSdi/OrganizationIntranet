namespace OrganizationIntranet.Models;

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

    public User? CreatedByUser { get; set; }
    public Application Application { get; set; } = null!;
    public ICollection<UserNotification> UserNotifications { get; set; } = new List<UserNotification>();
}
