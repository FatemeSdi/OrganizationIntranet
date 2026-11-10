namespace OrganizationIntranet.Contracts;

/// <summary>سه بخش ثابت کتابخانه‌ی سازمانی. دسته‌بندی‌ها داخل هر بخش را مدیر در پنل می‌سازد.</summary>
public static class LibraryGroups
{
    public const string Rules = "rules", Publications = "publications", Forms = "forms";
    public static readonly IReadOnlyList<(string Code, string Title)> All =
        [(Rules, "قوانین و مقررات"), (Publications, "انتشارات و پشتیبانی"), (Forms, "فرم‌های سازمانی")];
    public static bool IsValid(string? code) => All.Any(g => g.Code == code);
    public static string Title(string code) => All.FirstOrDefault(g => g.Code == code).Title ?? code;
}

/// <summary>قواعد فایل اسناد: پسوندهای مجاز (نوع واقعی از امضای فایل هم سنجیده می‌شود)، سقف حجم و سقف طول فیلدها.</summary>
public static class LibraryRules
{
    public const long DefaultMaxFileBytes = 20L * 1024 * 1024;
    public const int MaxTitleLength = 300, MaxDescriptionLength = 1000, MaxCategoryTitleLength = 200, PageSize = 20, MaxSearchLength = 100;
    public static readonly IReadOnlyList<string> Extensions = ["pdf", "doc", "docx", "xls", "xlsx", "ppt", "pptx", "txt", "zip", "rar", "png", "jpg", "jpeg", "tif", "tiff"];
    /// <summary>حداکثر شمار فایل در یک آپلود چندتایی و سقف کل حجم یک درخواست آپلود (حجم هر فایل جداگانه با MaxFileBytes سنجیده می‌شود).</summary>
    public const int MaxBatchFiles = 50;
    public const long MaxRequestBytes = 400L * 1024 * 1024;
    /// <summary>تصویرهای قابل پیش‌نمایش در مرورگر (tif را مرورگر نشان نمی‌دهد) و سقف حجم برای بندانگشتی، تا فهرست با عکس‌های بزرگ سنگین نشود.</summary>
    public static readonly IReadOnlyList<string> PreviewExtensions = ["png", "jpg", "jpeg"];
    public const long MaxPreviewBytes = 3L * 1024 * 1024;
    public static bool CanPreview(string? extension, long sizeBytes) => extension is not null && PreviewExtensions.Contains(extension.ToLowerInvariant()) && sizeBytes is > 0 and <= MaxPreviewBytes;
    /// <summary>مقدار accept برای input فایل در فرم مدیر.</summary>
    public static string Accept => string.Join(",", Extensions.Select(e => "." + e));
}

/// <summary>یک دسته‌بندی برای نمایش عمومی (با شمار سندهای فعال منطبق).</summary>
public record LibraryCategoryDto(int CategoryId, string GroupCode, string Title, int DocumentCount);
/// <summary>یک سند برای نمایش عمومی؛ خود فایل از نشانی دانلود می‌آید، نه از این رکورد.</summary>
public record LibraryDocumentDto(long DocumentId, int CategoryId, string GroupCode, string CategoryTitle, string Title, string? Description, string FileName, string Extension, long SizeBytes, DateTime UpdatedAt)
{
    /// <summary>تصویر کوچک‌تر از ۳ مگابایت (png یا jpg) که بندانگشتی دارد.</summary>
    public bool HasPreview => LibraryRules.CanPreview(Extension, SizeBytes);
}
/// <summary>دسته‌بندی‌های دارای سند فعال و (در صورت درخواست) سندهای منطبق با فیلتر بخش، دسته و جست‌وجو.</summary>
public record LibraryCatalogDto(List<LibraryCategoryDto> Categories, List<LibraryDocumentDto> Documents, int Total);

/// <summary>دسته‌بندی برای مدیر: همه‌ی دسته‌ها (فعال یا نه، خالی یا نه) با شمار همه‌ی سندها.</summary>
public record LibraryCategoryAdminDto(int CategoryId, string GroupCode, string Title, int DisplayOrder, bool IsActive, int DocumentCount);
/// <summary>ساخت یا ویرایش دسته‌بندی کتابخانه: بخش، عنوان، ترتیب نمایش و فعال بودن.</summary>
public record LibraryCategoryRequest(string? GroupCode, string? Title, int DisplayOrder = 0, bool IsActive = true);
/// <summary>سند برای مدیر، با وضعیت و سازنده.</summary>
public record LibraryDocumentAdminDto(long DocumentId, int CategoryId, string GroupCode, string CategoryTitle, string Title, string? Description, string FileName, string Extension,
    long SizeBytes, bool IsActive, DateTime CreatedAt, DateTime UpdatedAt, string UpdatedBy)
{
    public bool HasPreview => LibraryRules.CanPreview(Extension, SizeBytes);
}
/// <summary>یک صفحه از سندهای کتابخانه برای مدیر با شمار کل و صفحه‌بندی.</summary>
public record LibraryDocumentPageDto(List<LibraryDocumentAdminDto> Items, int Total, int Page, int PageSize)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(Total / (double)Math.Max(1, PageSize)));
}
/// <summary>مشخصات سند در ساخت یا ویرایش (فایل جدا و چندبخشی می‌آید).</summary>
public record LibraryDocumentRequest(int CategoryId, string? Title, string? Description, bool IsActive = true);
/// <summary>یک فایل ردشده در آپلود چندتایی و دلیل آن.</summary>
public record LibraryBatchFailureDto(string FileName, string Reason);
/// <summary>نتیجه‌ی آپلود چندتایی: هر فایل جداگانه ثبت یا رد می‌شود (رد یک فایل بقیه را متوقف نمی‌کند).</summary>
public record LibraryBatchResultDto(int Created, List<LibraryBatchFailureDto> Failures);
