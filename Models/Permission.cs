namespace OrganizationIntranet.Models;

public class Permission
{
    public int PermissionId { get; set; }
    public int ApplicationId { get; set; }
    public string PermissionName { get; set; } = string.Empty;
    public string PermissionCode { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }

    public Application Application { get; set; } = null!;
    public ICollection<UserPermission> UserPermissions { get; set; } = new List<UserPermission>();
    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}
