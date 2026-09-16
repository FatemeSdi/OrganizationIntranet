namespace OrganizationIntranet.Domain.Entities;

public class UserPermission
{
    public long UserPermissionId { get; set; }
    public long UserId { get; set; }
    public int PermissionId { get; set; }
    public DateTime CreatedAt { get; set; }

    public User User { get; set; } = null!;
    public Permission Permission { get; set; } = null!;
}
