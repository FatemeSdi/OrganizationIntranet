namespace OrganizationIntranet.Domain.Entities;

/// <summary>
/// یک واحد از چارت سازمانی (جدول Org.Unit). واحدها درختی‌اند: ریشه ParentId خالی دارد و هر واحد یک پدر دارد.
/// FullPath مسیر کامل از ریشه با «/» است و با هر تغییر نام یا جابه‌جایی برای خود واحد و زیرمجموعه‌هایش دوباره ساخته می‌شود.
/// </summary>
public class OrgUnit
{
    public const int MaxTitleLength = 200;
    public const int MaxPathLength = 1000;
    public const string PathSeparator = "/";

    public int UnitId { get; set; }
    public int? ParentId { get; set; }
    public string Title { get; set; } = "";
    public string FullPath { get; set; } = "";
    /// <summary>ترتیب نمایش میان واحدهای هم‌سطح (کوچک‌تر بالاتر).</summary>
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
    /// <summary>شناسه‌ی واحد در سامانه‌ی مبدأ (مثلاً ITServiceDesk) برای وارد کردن تکرارپذیر؛ برای واحدهای دستی خالی است.</summary>
    public long? SourceId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public long? UpdatedBy { get; set; }
    public OrgUnit? Parent { get; set; }
    public ICollection<OrgUnit> Children { get; set; } = new List<OrgUnit>();
    public ICollection<User> Users { get; set; } = new List<User>();
}
