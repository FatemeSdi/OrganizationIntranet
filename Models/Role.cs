namespace OrganizationIntranet.Models;

public class Role
{
    public int RoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public string RoleCode { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }

    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}
