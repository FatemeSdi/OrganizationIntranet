namespace OrganizationIntranet.Models;

public class UserNotification
{
    public long UserNotificationId { get; set; }
    public long NotificationId { get; set; }
    public long UserId { get; set; }
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime CreatedAt { get; set; }

    public Notification Notification { get; set; } = null!;
    public User User { get; set; } = null!;
}
