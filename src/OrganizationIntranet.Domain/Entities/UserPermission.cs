namespace OrganizationIntranet.Domain.Entities;

/// <summary>مجوز مستقیم کاربر (چند به چند). در کد استفاده نمی‌شود؛ تصمیم حذف یا نگه‌داشتن آن هنوز گرفته نشده است.</summary>
public class UserPermission
{
    public long UserPermissionId { get; set; }
    public long UserId { get; set; }
    public int PermissionId { get; set; }
    public DateTime CreatedAt { get; set; }

    public User User { get; set; } = null!;
    public Permission Permission { get; set; } = null!;
}
