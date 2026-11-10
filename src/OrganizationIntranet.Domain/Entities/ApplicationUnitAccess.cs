namespace OrganizationIntranet.Domain.Entities;

/// <summary>
/// واحدی از چارت سازمانی که سامانه‌ی «محدود به واحد» (Application.IsUnitRestricted) برای کاربرانش در دسترس است.
/// دسترسی شامل همه‌ی زیرواحدهای آن هم می‌شود (با پیشوند مسیر کامل واحد).
/// </summary>
public class ApplicationUnitAccess
{
    public long ApplicationUnitAccessId { get; set; }
    public int ApplicationId { get; set; }
    public int UnitId { get; set; }
    public DateTime CreatedAt { get; set; }
    public Application Application { get; set; } = null!;
    public OrgUnit Unit { get; set; } = null!;
}
