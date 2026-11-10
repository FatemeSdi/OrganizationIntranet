using OrganizationIntranet.Domain.Entities;

namespace OrganizationIntranet.Application.Abstractions;

/// <summary>دسته‌بندی همراه شمار سندها (برای فهرست‌ها).</summary>
public sealed record LibraryCategoryRow(LibraryCategory Category, int DocumentCount);

/// <summary>دسترسی به داده‌ی کتابخانه‌ی اسناد؛ پیاده‌سازی در لایه‌ی API.</summary>
public interface ILibraryRepository
{
    /// <summary>دسته‌های فعال که دست‌کم یک سند فعال دارند، با شمار سندهای فعال؛ فقط‌خواندنی.</summary>
    Task<List<LibraryCategoryRow>> PublicCategoriesAsync();
    /// <summary>سندهای فعالِ دسته‌ی فعال، فقط‌خواندنی (بدون محتوای فایل) با دسته‌شان.</summary>
    Task<List<LibraryDocument>> PublicDocumentsAsync();
    Task<LibraryDocument?> FindPublicDocumentAsync(long id);
    Task<List<LibraryCategoryRow>> AllCategoriesAsync();
    Task<LibraryCategory?> FindCategoryAsync(int id);
    Task<bool> CategoryTitleTakenAsync(string groupCode, string title, int? exceptId);
    Task<int> DocumentCountAsync(int categoryId);
    void AddCategory(LibraryCategory category);
    void RemoveCategory(LibraryCategory category);
    Task<(List<LibraryDocument> Items, int Total)> PageAsync(string? groupCode, int? categoryId, string? status, string? search, int page, int pageSize);
    Task<LibraryDocument?> FindDocumentAsync(long id);
    void AddDocument(LibraryDocument document);
    void RemoveDocument(LibraryDocument document);
    Task SaveAsync();
}

/// <summary>نگهداری فایل‌های اسناد؛ نام‌ها فقط StoredName تصادفی هستند و هیچ مسیری از ورودی کاربر ساخته نمی‌شود.</summary>
public interface ILibraryFileStore
{
    /// <summary>جریان را با نام داده‌شده ذخیره می‌کند و SHA-256 و شمار بایت‌های نوشته‌شده را برمی‌گرداند؛ بیش از maxBytes خطا و فایل ناقص پاک می‌شود.</summary>
    Task<(byte[] Sha256, long Size)> SaveAsync(string storedName, Stream content, long maxBytes, CancellationToken cancellationToken);
    /// <summary>جریان خواندن فایل؛ null اگر روی دیسک نباشد.</summary>
    Stream? OpenRead(string storedName);
    void Delete(string storedName);
}
