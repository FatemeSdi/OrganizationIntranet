namespace OrganizationIntranet.Domain.Entities;

/// <summary>نقش (جدول Sec.Role): سطح کلی دسترسی. ADMIN و EXPERT نقش‌های سیستمی‌اند و کدشان عوض یا حذف نمی‌شود.</summary>
public class Role
{
    public int RoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public string RoleCode { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }

    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}
