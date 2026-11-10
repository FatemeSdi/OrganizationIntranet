namespace OrganizationIntranet.Domain.Entities;

/// <summary>دسته‌بندی کتابخانه‌ی سازمانی (جدول Lib.Category) داخل یکی از سه بخش ثابت rules، publications یا forms.</summary>
public class LibraryCategory
{
    public int CategoryId { get; set; }
    public string GroupCode { get; set; } = "";
    public string Title { get; set; } = "";
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public ICollection<LibraryDocument> Documents { get; set; } = new List<LibraryDocument>();
}

/// <summary>
/// یک سند بارگذاری‌شده (جدول Lib.Document). خود فایل روی دیسک سرور API با نام بی‌معنی StoredName نگه داشته می‌شود، نه در بانک؛
/// FileName نام اصلی برای دانلود است، ContentType از پسوند مجاز تعیین می‌شود (نه از ورودی کاربر) و Sha256 برای صحت فایل است.
/// </summary>
public class LibraryDocument
{
    public long DocumentId { get; set; }
    public int CategoryId { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "application/octet-stream";
    public long SizeBytes { get; set; }
    /// <summary>نام فایل روی دیسک: ۳۲ نویسه‌ی هگز تصادفی (Guid)؛ هیچ بخشی از آن از ورودی کاربر نیست.</summary>
    public string StoredName { get; set; } = "";
    public byte[] Sha256 { get; set; } = [];
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public long CreatedBy { get; set; }
    public long UpdatedBy { get; set; }
    public LibraryCategory Category { get; set; } = null!;
    public User? UpdatedByUser { get; set; }
}
