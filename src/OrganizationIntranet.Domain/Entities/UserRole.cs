namespace OrganizationIntranet.Domain.Entities;

/// <summary>عضویت یک کاربر در یک نقش (چند به چند).</summary>
public class UserRole
{
    public long UserRoleId { get; set; }
    public long UserId { get; set; }
    public int RoleId { get; set; }
    public DateTime CreatedAt { get; set; }

    public User User { get; set; } = null!;
    public Role Role { get; set; } = null!;
}
