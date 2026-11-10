using OrganizationIntranet.Application.Abstractions;
using OrganizationIntranet.Contracts;
using OrganizationIntranet.Domain.Entities;

namespace OrganizationIntranet.Application.Services;

/// <summary>
/// کتابخانه‌ی اسناد سازمان (قوانین و مقررات، انتشارات، فرم‌ها): فهرست عمومی با جست‌وجو، دانلود، و مدیریت دسته‌بندی و سند.
/// فایل روی دیسک سرور (ILibraryFileStore) و فقط مشخصاتش در بانک است. پسوند باید در فهرست مجاز باشد، «امضای» واقعی فایل با پسوند بخواند
/// (فایل اجرایی با پسوند pdf رد می‌شود) و نوع محتوا را خود سرور از پسوند تعیین می‌کند، نه کاربر.
/// </summary>
public sealed class LibraryService(ILibraryRepository repository, ILibraryFileStore files)
{
    private static readonly Dictionary<string, string> ContentTypes = new()
    {
        ["pdf"] = "application/pdf", ["doc"] = "application/msword", ["docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ["xls"] = "application/vnd.ms-excel", ["xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        ["ppt"] = "application/vnd.ms-powerpoint", ["pptx"] = "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        ["txt"] = "text/plain", ["zip"] = "application/zip", ["rar"] = "application/vnd.rar", ["png"] = "image/png", ["jpg"] = "image/jpeg", ["jpeg"] = "image/jpeg",
        ["tif"] = "image/tiff", ["tiff"] = "image/tiff",
    };

    /// <summary>یکسان‌سازی برای جست‌وجو: ی/ک عربی، نیم‌فاصله و ارقام فارسی.</summary>
    public static string Normalize(string? text) => string.Concat((text ?? "").Trim().ToLowerInvariant()
        .Replace('ي', 'ی').Replace('ك', 'ک').Replace('‌', ' ')
        .Select(c => c is >= '۰' and <= '۹' ? (char)('0' + (c - '۰')) : c));

    private static string Extension(string fileName) => Path.GetExtension(fileName).TrimStart('.').ToLowerInvariant();

    /// <summary>آیا ابتدای فایل با امضای پسوند ادعاشده می‌خواند؟ (پایه‌ی رد فایل مخرب با پسوند جعلی)</summary>
    internal static bool SignatureMatches(string extension, ReadOnlySpan<byte> head)
    {
        static bool Starts(ReadOnlySpan<byte> head, params byte[] prefix) => head.Length >= prefix.Length && head[..prefix.Length].SequenceEqual(prefix);
        switch (extension)
        {
            case "pdf": return Starts(head, 0x25, 0x50, 0x44, 0x46);                                       // %PDF
            case "png": return Starts(head, 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A);
            case "jpg" or "jpeg": return Starts(head, 0xFF, 0xD8, 0xFF);
            case "zip" or "docx" or "xlsx" or "pptx": return Starts(head, 0x50, 0x4B, 0x03, 0x04) || Starts(head, 0x50, 0x4B, 0x05, 0x06);
            case "doc" or "xls" or "ppt": return Starts(head, 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1);
            case "rar": return Starts(head, 0x52, 0x61, 0x72, 0x21, 0x1A, 0x07);                         // Rar!\x1A\x07 (RAR4 و RAR5)
            case "tif" or "tiff": return Starts(head, 0x49, 0x49, 0x2A, 0x00) || Starts(head, 0x4D, 0x4D, 0x00, 0x2A);   // little-endian یا big-endian
            case "txt": return head.Length > 0 && !head.Contains((byte)0);                                // متن ساده: بایت صفر ندارد
            default: return false;
        }
    }

    /// <summary>نام فایل برای نمایش و دانلود: بدون مسیر، بدون نویسه‌ی کنترلی و ممنوع در نام فایل ویندوز، حداکثر ۲۰۰ نویسه.</summary>
    internal static string SafeFileName(string? name)
    {
        var cleaned = string.Concat((name ?? "").Replace('\\', '/').Split('/').Last().Where(c => !char.IsControl(c)).Select(c => "\"<>|:*?".Contains(c) ? '_' : c)).Trim().Trim('.');
        if (cleaned.Length > 200) { var ext = Path.GetExtension(cleaned); cleaned = cleaned[..(200 - ext.Length)] + ext; }
        return cleaned;
    }

    private static string RequiredText(string? value, int max, string label)
    {
        value = value?.Trim();
        if (string.IsNullOrEmpty(value) || value.Length > max) throw new ArgumentException($"{label} الزامی است و حداکثر {max} کاراکتر دارد");
        return value;
    }

    private static LibraryDocumentDto Map(LibraryDocument d) => new(d.DocumentId, d.CategoryId, d.Category.GroupCode, d.Category.Title, d.Title, d.Description, d.FileName, Extension(d.FileName), d.SizeBytes, d.UpdatedAt);
    private static LibraryDocumentAdminDto MapAdmin(LibraryDocument d) => new(d.DocumentId, d.CategoryId, d.Category.GroupCode, d.Category.Title, d.Title, d.Description, d.FileName, Extension(d.FileName),
        d.SizeBytes, d.IsActive, d.CreatedAt, d.UpdatedAt, d.UpdatedByUser is null ? "" : $"{d.UpdatedByUser.Name} {d.UpdatedByUser.LastName}".Trim());

    // ----- نمایش عمومی -----

    /// <summary>
    /// دسته‌های دارای سند فعال با شمار سندهای منطبق و (اگر includeDocuments) خود سندها. group، categoryId و جست‌وجو (عنوان، توضیح، نام دسته و نام فایل) را اعمال می‌کند؛
    /// بخش یا جست‌وجوی نامعتبر رد می‌شود. سند دسته‌ی غیرفعال یا سند غیرفعال هرگز برنمی‌گردد.
    /// </summary>
    public async Task<LibraryCatalogDto> CatalogAsync(string? group, int? categoryId, string? search, bool includeDocuments)
    {
        if (group is not null && !LibraryGroups.IsValid(group)) throw new ArgumentException("بخش کتابخانه معتبر نیست");
        if (search?.Length > LibraryRules.MaxSearchLength) throw new ArgumentException("جست‌وجو حداکثر ۱۰۰ نویسه است");
        var needle = string.IsNullOrWhiteSpace(search) ? null : Normalize(search);
        var documents = (await repository.PublicDocumentsAsync())
            .Where(d => (group is null || d.Category.GroupCode == group) && (categoryId is null || d.CategoryId == categoryId)
                && (needle is null || Normalize(d.Title).Contains(needle, StringComparison.Ordinal) || Normalize(d.Description).Contains(needle, StringComparison.Ordinal)
                    || Normalize(d.Category.Title).Contains(needle, StringComparison.Ordinal) || Normalize(d.FileName).Contains(needle, StringComparison.Ordinal)))
            .ToList();
        var counts = documents.GroupBy(d => d.CategoryId).ToDictionary(g => g.Key, g => g.Count());
        var categories = (await repository.PublicCategoriesAsync()).Where(c => counts.ContainsKey(c.Category.CategoryId))
            .OrderBy(c => c.Category.GroupCode).ThenBy(c => c.Category.DisplayOrder).ThenBy(c => c.Category.CategoryId)
            .Select(c => new LibraryCategoryDto(c.Category.CategoryId, c.Category.GroupCode, c.Category.Title, counts[c.Category.CategoryId])).ToList();
        return new(categories, includeDocuments
            ? documents.OrderBy(d => d.Category.GroupCode).ThenBy(d => d.Category.DisplayOrder).ThenBy(d => d.Title, StringComparer.Ordinal).Select(Map).ToList() : [], documents.Count);
    }

    /// <summary>سند فعال برای دانلود و جریان فایلش. سند ناموجود، غیرفعال یا بدون فایل روی دیسک «یافت نشد» است.</summary>
    public async Task<(LibraryDocumentDto Document, string ContentType, Stream Content)> OpenAsync(long id)
    {
        var document = await repository.FindPublicDocumentAsync(id) ?? throw new KeyNotFoundException();
        var stream = files.OpenRead(document.StoredName) ?? throw new KeyNotFoundException();
        return (Map(document), document.ContentType, stream);
    }

    /// <summary>فایل هر سندی (فعال یا غیرفعال) برای مدیر؛ ناموجود یا بدون فایل روی دیسک «یافت نشد» است.</summary>
    /// <summary>
    /// پیش‌نمایش درون‌خطی یک تصویر (png یا jpg تا ۳ مگابایت) برای فهرست‌ها. فقط همین تصویرها؛ برای هر سند دیگر (و tif که مرورگر نشان نمی‌دهد) «یافت نشد» است.
    /// نمایش عمومی فقط سند فعالِ دسته‌ی فعال را می‌دهد و مدیر (forAdmin) هر سندی را. نوع محتوا از پسوند و امضای تأییدشده‌ی سرور می‌آید.
    /// </summary>
    public async Task<(string ContentType, Stream Content)> OpenPreviewAsync(long id, bool forAdmin)
    {
        var document = forAdmin ? await repository.FindDocumentAsync(id) : await repository.FindPublicDocumentAsync(id);
        if (document is null || !LibraryRules.CanPreview(Extension(document.FileName), document.SizeBytes)) throw new KeyNotFoundException();
        return (document.ContentType, files.OpenRead(document.StoredName) ?? throw new KeyNotFoundException());
    }

    public async Task<(string FileName, string ContentType, Stream Content)> OpenForAdminAsync(long id)
    {
        var document = await repository.FindDocumentAsync(id) ?? throw new KeyNotFoundException();
        return (document.FileName, document.ContentType, files.OpenRead(document.StoredName) ?? throw new KeyNotFoundException());
    }

    // ----- مدیریت دسته‌بندی -----

    public async Task<List<LibraryCategoryAdminDto>> CategoriesAsync() => (await repository.AllCategoriesAsync())
        .OrderBy(c => c.Category.GroupCode).ThenBy(c => c.Category.DisplayOrder).ThenBy(c => c.Category.CategoryId)
        .Select(c => new LibraryCategoryAdminDto(c.Category.CategoryId, c.Category.GroupCode, c.Category.Title, c.Category.DisplayOrder, c.Category.IsActive, c.DocumentCount)).ToList();

    private async Task ApplyAsync(LibraryCategory category, LibraryCategoryRequest request)
    {
        if (!LibraryGroups.IsValid(request.GroupCode)) throw new ArgumentException("بخش کتابخانه معتبر نیست");
        var title = RequiredText(request.Title, LibraryRules.MaxCategoryTitleLength, "عنوان دسته");
        if (request.DisplayOrder is < 0 or > 10000) throw new ArgumentException("ترتیب نمایش باید بین ۰ تا ۱۰۰۰۰ باشد");
        if (await repository.CategoryTitleTakenAsync(request.GroupCode!, title, category.CategoryId == 0 ? null : category.CategoryId)) throw new ArgumentException("در این بخش دسته‌ای با همین عنوان هست");
        category.GroupCode = request.GroupCode!; category.Title = title; category.DisplayOrder = request.DisplayOrder; category.IsActive = request.IsActive;
    }
    public async Task CreateCategoryAsync(LibraryCategoryRequest request)
    {
        var category = new LibraryCategory { CreatedAt = DateTime.UtcNow };
        await ApplyAsync(category, request); repository.AddCategory(category); await repository.SaveAsync();
    }
    public async Task UpdateCategoryAsync(int id, LibraryCategoryRequest request)
    {
        var category = await repository.FindCategoryAsync(id) ?? throw new KeyNotFoundException();
        await ApplyAsync(category, request); await repository.SaveAsync();
    }
    /// <summary>حذف دسته فقط وقتی هیچ سندی (حتی غیرفعال) ندارد؛ وگرنه غیرفعالش کنید.</summary>
    public async Task DeleteCategoryAsync(int id)
    {
        var category = await repository.FindCategoryAsync(id) ?? throw new KeyNotFoundException();
        if (await repository.DocumentCountAsync(id) > 0) throw new ArgumentException("این دسته سند دارد؛ ابتدا سندها را جابه‌جا یا حذف کنید، یا دسته را غیرفعال کنید");
        repository.RemoveCategory(category); await repository.SaveAsync();
    }

    // ----- مدیریت سند -----

    public async Task<LibraryDocumentPageDto> DocumentsAsync(string? group, int? categoryId, string? status, string? search, int page)
    {
        if (group is not null && !LibraryGroups.IsValid(group)) throw new ArgumentException("بخش کتابخانه معتبر نیست");
        if (status is not (null or "" or "active" or "inactive")) throw new ArgumentException("وضعیت سند معتبر نیست");
        if (page is < 1 or > 100000 || search?.Length > LibraryRules.MaxSearchLength) throw new ArgumentException("پارامترهای جست‌وجو معتبر نیستند");
        var (items, total) = await repository.PageAsync(group, categoryId, string.IsNullOrEmpty(status) ? null : status, search?.Trim(), page, LibraryRules.PageSize);
        return new(items.Select(MapAdmin).ToList(), total, page, LibraryRules.PageSize);
    }
    public async Task<LibraryDocumentAdminDto> GetDocumentAsync(long id) => MapAdmin(await repository.FindDocumentAsync(id) ?? throw new KeyNotFoundException());

    /// <summary>پسوند و امضای فایل را می‌سنجد و برمی‌گرداند (پسوند کوچک‌شده، نوع محتوا، نام امن). جریان باید قابل بازگشت به ابتدا باشد.</summary>
    private static (string Extension, string ContentType, string FileName) Inspect(string? fileName, Stream content, long length, long maxBytes)
    {
        var name = SafeFileName(fileName); var ext = Extension(name);
        if (length <= 0) throw new ArgumentException("فایل خالی است");
        if (length > maxBytes) throw new ArgumentException($"حجم فایل باید حداکثر {maxBytes / (1024 * 1024)} مگابایت باشد");
        if (!ContentTypes.TryGetValue(ext, out var contentType)) throw new ArgumentException("نوع فایل مجاز نیست؛ پسوندهای مجاز: " + string.Join("، ", LibraryRules.Extensions));
        var head = new byte[4096]; var read = 0;
        for (int n; read < head.Length && (n = content.Read(head, read, head.Length - read)) > 0;) read += n;
        content.Position = 0;
        if (!SignatureMatches(ext, head.AsSpan(0, read))) throw new ArgumentException("محتوای فایل با پسوند آن نمی‌خواند");
        return (ext, contentType, name);
    }

    private async Task<(string StoredName, byte[] Hash, long Size)> StoreAsync(Stream content, long maxBytes, CancellationToken cancellationToken)
    {
        var storedName = Guid.NewGuid().ToString("N");
        var (hash, size) = await files.SaveAsync(storedName, content, maxBytes, cancellationToken);
        return (storedName, hash, size);
    }

    /// <summary>سند جدید با فایل. اگر ذخیره‌ی بانک شکست بخورد فایل نوشته‌شده پاک می‌شود تا فایل یتیم نماند.</summary>
    public async Task<long> CreateDocumentAsync(LibraryDocumentRequest request, string? fileName, Stream content, long length, long maxBytes, long actor, CancellationToken cancellationToken = default)
    {
        if (actor <= 0) throw new ArgumentException("کاربر معتبر نیست");
        var title = RequiredText(request.Title, LibraryRules.MaxTitleLength, "عنوان سند");
        var description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        if (description?.Length > LibraryRules.MaxDescriptionLength) throw new ArgumentException($"توضیحات حداکثر {LibraryRules.MaxDescriptionLength} کاراکتر دارد");
        _ = await repository.FindCategoryAsync(request.CategoryId) ?? throw new ArgumentException("دسته‌بندی انتخاب‌شده وجود ندارد");
        var (_, contentType, safeName) = Inspect(fileName, content, length, maxBytes);
        var (storedName, hash, size) = await StoreAsync(content, maxBytes, cancellationToken);
        var now = DateTime.UtcNow;
        var document = new LibraryDocument { CategoryId = request.CategoryId, Title = title, Description = description, FileName = safeName, ContentType = contentType, SizeBytes = size,
            StoredName = storedName, Sha256 = hash, IsActive = request.IsActive, CreatedAt = now, UpdatedAt = now, CreatedBy = actor, UpdatedBy = actor };
        try { repository.AddDocument(document); await repository.SaveAsync(); }
        catch { files.Delete(storedName); throw; }
        return document.DocumentId;
    }

    /// <summary>یک فایل از آپلود چندتایی؛ Open جریان خواندن را در لحظه‌ی نیاز می‌سازد (قابل بازگشت به ابتدا).</summary>
    public sealed record LibraryUpload(string? FileName, long Length, Func<Stream> Open);

    /// <summary>
    /// آپلود چندتایی در یک دسته با مشخصات مشترک. عنوان هر سند از نام فایلش (بدون پسوند) گرفته می‌شود. هر فایل مستقل بررسی و ثبت می‌شود:
    /// فایل نامعتبر (پسوند، امضا، حجم، خالی) فقط خودش رد می‌شود و دلیلش در نتیجه می‌آید. بیش از <see cref="LibraryRules.MaxBatchFiles"/> فایل یا دسته‌ی ناموجود کل درخواست را رد می‌کند.
    /// </summary>
    public async Task<LibraryBatchResultDto> CreateManyAsync(int categoryId, string? description, bool isActive, IReadOnlyList<LibraryUpload> uploads, long maxBytes, long actor, CancellationToken cancellationToken = default)
    {
        if (uploads.Count == 0) throw new ArgumentException("فایلی انتخاب نشده است");
        if (uploads.Count > LibraryRules.MaxBatchFiles) throw new ArgumentException($"در هر آپلود حداکثر {LibraryRules.MaxBatchFiles} فایل مجاز است");
        _ = await repository.FindCategoryAsync(categoryId) ?? throw new ArgumentException("دسته‌بندی انتخاب‌شده وجود ندارد");
        var created = 0; var failures = new List<LibraryBatchFailureDto>();
        foreach (var upload in uploads)
        {
            var name = SafeFileName(upload.FileName);
            try
            {
                var title = Path.GetFileNameWithoutExtension(name).Trim();
                if (title.Length > LibraryRules.MaxTitleLength) title = title[..LibraryRules.MaxTitleLength];
                await using var stream = upload.Open();
                await CreateDocumentAsync(new(categoryId, title, description, isActive), upload.FileName, stream, upload.Length, maxBytes, actor, cancellationToken);
                created++;
            }
            catch (ArgumentException ex) { failures.Add(new(name.Length == 0 ? "(بدون نام)" : name, ex.Message)); }
        }
        return new(created, failures);
    }

    /// <summary>ویرایش مشخصات سند و (اختیاری) جایگزینی فایل. فایل قدیمی فقط بعد از موفقیت ذخیره‌ی بانک پاک می‌شود.</summary>
    public async Task UpdateDocumentAsync(long id, LibraryDocumentRequest request, string? fileName, Stream? content, long length, long maxBytes, long actor, CancellationToken cancellationToken = default)
    {
        if (actor <= 0) throw new ArgumentException("کاربر معتبر نیست");
        var document = await repository.FindDocumentAsync(id) ?? throw new KeyNotFoundException();
        var title = RequiredText(request.Title, LibraryRules.MaxTitleLength, "عنوان سند");
        var description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        if (description?.Length > LibraryRules.MaxDescriptionLength) throw new ArgumentException($"توضیحات حداکثر {LibraryRules.MaxDescriptionLength} کاراکتر دارد");
        _ = await repository.FindCategoryAsync(request.CategoryId) ?? throw new ArgumentException("دسته‌بندی انتخاب‌شده وجود ندارد");
        string? oldStoredName = null, newStoredName = null;
        if (content is not null && length > 0)
        {
            var (_, contentType, safeName) = Inspect(fileName, content, length, maxBytes);
            var (storedName, hash, size) = await StoreAsync(content, maxBytes, cancellationToken);
            oldStoredName = document.StoredName; newStoredName = storedName;
            document.FileName = safeName; document.ContentType = contentType; document.SizeBytes = size; document.StoredName = storedName; document.Sha256 = hash;
        }
        document.CategoryId = request.CategoryId; document.Title = title; document.Description = description; document.IsActive = request.IsActive;
        document.UpdatedAt = DateTime.UtcNow; document.UpdatedBy = actor;
        try { await repository.SaveAsync(); }
        catch { if (newStoredName is not null) files.Delete(newStoredName); throw; }
        if (oldStoredName is not null) files.Delete(oldStoredName);
    }

    /// <summary>فعال یا غیرفعال‌کردن سند (غیرفعال از نمایش عمومی محو می‌شود ولی در بانک و دیسک می‌ماند).</summary>
    public async Task SetDocumentActiveAsync(long id, bool active, long actor)
    {
        var document = await repository.FindDocumentAsync(id) ?? throw new KeyNotFoundException();
        if (document.IsActive == active) return;
        document.IsActive = active; document.UpdatedAt = DateTime.UtcNow; document.UpdatedBy = actor;
        await repository.SaveAsync();
    }

    /// <summary>حذف دائمی سند و فایلش؛ برگشت‌ناپذیر است (برای موقت‌پنهان‌کردن، غیرفعال کنید).</summary>
    public async Task DeleteDocumentAsync(long id)
    {
        var document = await repository.FindDocumentAsync(id) ?? throw new KeyNotFoundException();
        var storedName = document.StoredName;
        repository.RemoveDocument(document);
        await repository.SaveAsync();
        files.Delete(storedName);
    }
}
