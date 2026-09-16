namespace OrganizationIntranet.Domain.Entities;

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
    public string SecurityStamp { get; set; } = Guid.NewGuid().ToString("N");
    public string? AuthenticatorSecret { get; set; }
    public long LastAuthenticatorStep { get; set; } = -1;
    public int FailedLoginAttempts { get; set; }
    public DateTime? LockedUntil { get; set; }
    public DateTime? LastSecuritySmsAt { get; set; }
    public Guid AuthenticationVersion { get; set; } = Guid.NewGuid();
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLogin { get; set; }

    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public ICollection<UserPermission> UserPermissions { get; set; } = new List<UserPermission>();
    public ICollection<UserNotification> UserNotifications { get; set; } = new List<UserNotification>();
    public ICollection<Notification> CreatedNotifications { get; set; } = new List<Notification>();
}
