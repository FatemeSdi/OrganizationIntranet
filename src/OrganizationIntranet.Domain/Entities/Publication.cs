namespace OrganizationIntranet.Domain.Entities;

public sealed class Publication
{
    public long PublicationId { get; set; }
    public string Title { get; set; } = "";
    public string Summary { get; set; } = "";
    public string Body { get; set; } = "";
    public string Kind { get; set; } = "News";
    public bool IsPublished { get; set; }
    /// <summary>نمایش برای بازدیدکننده‌ی ناشناس (بدون ورود). فقط برای محتوای منتشرشده مجاز است (قید CK_Publication_PublicRequiresPublished).</summary>
    public bool IsPublic { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? PublishedAt { get; set; }
    public long CreatedBy { get; set; }
    public long UpdatedBy { get; set; }
    public long Revision { get; set; }
    /// <summary>زمان آخرین تغییر تصویر شاخص (PublicationImage)؛ null یعنی مطلب تصویر ندارد. برای نسخه‌گذاری نشانی تصویر هم استفاده می‌شود.</summary>
    public DateTime? ImageUpdatedAt { get; set; }
}
