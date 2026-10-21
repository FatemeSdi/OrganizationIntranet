using System.ComponentModel.DataAnnotations;

namespace OrganizationIntranet.Contracts;

public static class PublicationKinds
{
    public static bool IsValid(string? kind) => kind is "News" or "Announcement" or "Circular";
    public static string Label(string kind) => kind switch { "News" => "خبر", "Announcement" => "اطلاعیه", "Circular" => "بخشنامه", _ => "نامعتبر" };
}
public sealed class PublicationEditRequest
{
    [Required, StringLength(200)] public string Title { get; set; } = "";
    [Required, StringLength(1000)] public string Summary { get; set; } = "";
    [Required, StringLength(50000)] public string Body { get; set; } = "";
    [Required] public string Kind { get; set; } = "News";
    public bool IsPublished { get; set; }
    /// <summary>نمایش برای بازدیدکننده‌ی ناشناس؛ فقط همراه با انتشار پذیرفته می‌شود.</summary>
    public bool IsPublic { get; set; }
    [Range(0, long.MaxValue)] public long? Revision { get; set; }
}
/// <param name="ImageVersion">نسخه‌ی تصویر شاخص (برای نشانی تصویر و کش)؛ 0 یعنی مطلب تصویر ندارد.</param>
public record PublicationDto(long PublicationId, string Title, string Summary, string Body, string Kind, bool IsPublished, DateTime CreatedAt, DateTime UpdatedAt, DateTime? PublishedAt, long Revision = 0, bool IsPublic = false, long ImageVersion = 0)
{
    public bool HasImage => ImageVersion != 0;
}
/// <summary>بارگذاری تصویر شاخص؛ نوع فایل از امضای خود فایل تشخیص داده می‌شود.</summary>
public sealed record PublicationImageUpload(string Base64);
public static class PublicationImageRules
{
    public const int MaxBytes = 2 * 1024 * 1024;
    public const string Accept = "image/jpeg,image/png,image/webp";
}
public record PublicationListDto(List<PublicationDto> Items, int Total, int Page, int PageSize);
