using System.ComponentModel.DataAnnotations;

namespace OrganizationIntranet.Contracts;

/// <summary>نوع مطلب: News (خبر)، Announcement (اطلاعیه) و Circular (بخشنامه) با برچسب فارسی.</summary>
public static class PublicationKinds
{
    public static bool IsValid(string? kind) => kind is "News" or "Announcement" or "Circular";
    public static string Label(string kind) => kind switch { "News" => "خبر", "Announcement" => "اطلاعیه", "Circular" => "بخشنامه", _ => "نامعتبر" };
}
/// <summary>ساخت یا ویرایش مطلب؛ سقف طول‌ها با ستون‌های بانک و فرم یکی است (عنوان ۲۰۰، خلاصه ۱۰۰۰، متن ۵۰۰۰۰). Revision در ویرایش برای تشخیص تعارض لازم است.</summary>
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
    /// <summary>نمایش در اسلایدر اخبار صفحه‌ی اصلی (فقط وقتی منتشر شده باشد دیده می‌شود).</summary>
    public bool ShowInSlider { get; set; } = true;
    /// <summary>جایگاه در اسلایدر، ۰ تا ۱۰۰۰۰؛ عدد کوچک‌تر جلوتر. با برابری، مطلب تازه‌تر جلوتر است.</summary>
    [Range(0, 10000)] public int SlideOrder { get; set; }
}
/// <param name="ImageVersion">نسخه‌ی تصویر شاخص (برای نشانی تصویر و کش)؛ 0 یعنی مطلب تصویر ندارد.</param>
public record PublicationDto(long PublicationId, string Title, string Summary, string Body, string Kind, bool IsPublished, DateTime CreatedAt, DateTime UpdatedAt, DateTime? PublishedAt, long Revision = 0, bool IsPublic = false, long ImageVersion = 0, bool IsArchived = false, bool ShowInSlider = true, int SlideOrder = 0)
{
    public bool HasImage => ImageVersion != 0;
}
/// <summary>بارگذاری تصویر شاخص؛ نوع فایل از امضای خود فایل تشخیص داده می‌شود.</summary>
public sealed record PublicationImageUpload(string Base64);
/// <summary>قواعد تصویر شاخص: حداکثر ۲ مگابایت و فقط JPEG، PNG یا WebP.</summary>
public static class PublicationImageRules
{
    public const int MaxBytes = 2 * 1024 * 1024;
    public const string Accept = "image/jpeg,image/png,image/webp";
}
/// <summary>صفحه‌ای از مطالب (۲۰ مورد در هر صفحه).</summary>
public record PublicationListDto(List<PublicationDto> Items, int Total, int Page, int PageSize);
