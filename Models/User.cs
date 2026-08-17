namespace OrganizationIntranet.Models;

public class User
{
    public long UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? NationalId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? MobileNumber { get; set; }
    public string? Email { get; set; }
    public string? PasswordHash { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLogin { get; set; }

    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public ICollection<UserPermission> UserPermissions { get; set; } = new List<UserPermission>();
    public ICollection<UserNotification> UserNotifications { get; set; } = new List<UserNotification>();
    public ICollection<Notification> CreatedNotifications { get; set; } = new List<Notification>();
}
