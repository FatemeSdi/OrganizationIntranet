using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrganizationIntranet.Application.Services;
using OrganizationIntranet.Contracts;
using P = OrganizationIntranet.Contracts.AdminPermissions;

namespace OrganizationIntranet.Api.Controllers;

/// <summary>فرم چندبخشی ساخت یا ویرایش سند؛ File در ویرایش اختیاری است (خالی = فایل فعلی می‌ماند).</summary>
public sealed class LibraryDocumentForm
{
    public int CategoryId { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public IFormFile? File { get; set; }
}

/// <summary>فرم چندبخشی آپلود چندتایی: مشخصات مشترک همه‌ی فایل‌ها (دسته، توضیح، فعال بودن) و فایل‌ها با نام فیلد Files.</summary>
public sealed class LibraryBatchForm
{
    public int CategoryId { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public List<IFormFile>? Files { get; set; }
}

/// <summary>
/// API مدیریت کتابخانه‌ی اسناد در پنل مدیریت (دسته‌بندی‌ها و سندها). فقط از Admin (با کلید مدیریتی)، برای دارندگان INTRANET_ADMIN و مجوز LIBRARY_EDIT (زنده از بانک).
/// آپلود چندبخشی است؛ سقف حجم از <c>Library:MaxFileBytes</c> (پیش‌فرض ۲۰ مگابایت) و پسوند و امضای فایل در سرویس بررسی می‌شود.
/// </summary>
[OrganizationIntranet.Api.Security.AdminClient]
[ApiController, Route("api/admin/library"), Authorize(Policy = P.IntranetAdminPolicy), Authorize(Policy = P.LibraryEditPolicy)]
public sealed class AdminLibraryController(LibraryService service, IConfiguration configuration, ILogger<AdminLibraryController> logger) : ControllerBase
{
    /// <summary>شناسه‌ی مدیر انجام‌دهنده؛ برای ثبت در سند و لاگ.</summary>
    private long Actor => long.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
    private long MaxBytes => configuration.GetValue<long?>("Library:MaxFileBytes") is > 0 and var configured ? configured : LibraryRules.DefaultMaxFileBytes;

    /// <summary>همه‌ی دسته‌بندی‌ها (فعال یا نه، خالی یا نه) با شمار سندها.</summary>
    [HttpGet("categories")] public Task<List<LibraryCategoryAdminDto>> Categories() => service.CategoriesAsync();
    [HttpPost("categories")] public async Task<OperationResult> CreateCategory(LibraryCategoryRequest request)
    { await service.CreateCategoryAsync(request); logger.LogInformation("Library category created by {ActorId}", Actor); return new(true, "دسته‌بندی ساخته شد"); }
    [HttpPost("categories/{id:int}")] public async Task<OperationResult> UpdateCategory(int id, LibraryCategoryRequest request)
    { await service.UpdateCategoryAsync(id, request); logger.LogInformation("Library category {CategoryId} updated by {ActorId}", id, Actor); return new(true, "دسته‌بندی ذخیره شد"); }
    /// <summary>حذف دسته‌ی بدون سند.</summary>
    [HttpPost("categories/{id:int}/delete")] public async Task<OperationResult> DeleteCategory(int id)
    { await service.DeleteCategoryAsync(id); logger.LogInformation("Library category {CategoryId} deleted by {ActorId}", id, Actor); return new(true, "دسته‌بندی حذف شد"); }

    /// <summary>سندها از جدیدترین، با فیلتر بخش، دسته، وضعیت (active یا inactive) و جست‌وجو.</summary>
    [HttpGet("documents")] public Task<LibraryDocumentPageDto> Documents(string? group = null, int? category = null, string? status = null, string? search = null, int page = 1)
        => service.DocumentsAsync(group, category, status, search, page);
    [HttpGet("documents/{id:long}")] public Task<LibraryDocumentAdminDto> Document(long id) => service.GetDocumentAsync(id);

    /// <summary>دانلود فایل هر سند (حتی غیرفعال) برای مدیر؛ همیشه attachment.</summary>
    [HttpGet("documents/{id:long}/file")]
    public async Task<IActionResult> DocumentFile(long id)
    {
        var (fileName, contentType, content) = await service.OpenForAdminAsync(id);
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(content, contentType, fileName);
    }

    /// <summary>پیش‌نمایش درون‌خطی تصویر سند (png یا jpg تا ۳ مگابایت) برای مدیر، حتی وقتی سند غیرفعال است؛ برای غیرتصویر ۴۰۴.</summary>
    [HttpGet("documents/{id:long}/preview")]
    public async Task<IActionResult> DocumentPreview(long id)
    {
        var (contentType, content) = await service.OpenPreviewAsync(id, forAdmin: true);
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers["Content-Security-Policy"] = "default-src 'none'; sandbox";
        Response.Headers.CacheControl = "private, max-age=300";
        return File(content, contentType);
    }

    /// <summary>آپلود چندتایی در یک دسته (حداکثر ۵۰ فایل): عنوان هر سند از نام فایلش؛ هر فایل مستقل ثبت یا رد می‌شود و نتیجه شمار ثبت‌شده و دلیل ردها را می‌دهد.</summary>
    [HttpPost("documents/batch"), RequestSizeLimit(LibraryRules.MaxRequestBytes), RequestFormLimits(MultipartBodyLengthLimit = LibraryRules.MaxRequestBytes)]
    public async Task<LibraryBatchResultDto> CreateDocuments([FromForm] LibraryBatchForm form, CancellationToken cancellationToken)
    {
        var uploads = (form.Files ?? []).Select(f => new LibraryService.LibraryUpload(f.FileName, f.Length, f.OpenReadStream)).ToList();
        var result = await service.CreateManyAsync(form.CategoryId, form.Description, form.IsActive, uploads, MaxBytes, Actor, cancellationToken);
        logger.LogInformation("Library batch upload by {ActorId}: {Created} created, {Failed} rejected", Actor, result.Created, result.Failures.Count);
        return result;
    }

    /// <summary>سند جدید همراه فایل (الزامی).</summary>
    [HttpPost("documents"), RequestSizeLimit(60_000_000)]
    public async Task<OperationResult> CreateDocument([FromForm] LibraryDocumentForm form, CancellationToken cancellationToken)
    {
        if (form.File is null || form.File.Length == 0) throw new ArgumentException("فایل سند را انتخاب کنید");
        await using var stream = form.File.OpenReadStream();
        var id = await service.CreateDocumentAsync(new(form.CategoryId, form.Title, form.Description, form.IsActive), form.File.FileName, stream, form.File.Length, MaxBytes, Actor, cancellationToken);
        logger.LogInformation("Library document {DocumentId} created by {ActorId}", id, Actor);
        return new(true, "سند ثبت شد");
    }

    /// <summary>ویرایش مشخصات سند و (اگر File بیاید) جایگزینی فایل.</summary>
    [HttpPost("documents/{id:long}"), RequestSizeLimit(60_000_000)]
    public async Task<OperationResult> UpdateDocument(long id, [FromForm] LibraryDocumentForm form, CancellationToken cancellationToken)
    {
        await using var stream = form.File is { Length: > 0 } ? form.File.OpenReadStream() : null;
        await service.UpdateDocumentAsync(id, new(form.CategoryId, form.Title, form.Description, form.IsActive), form.File?.FileName, stream, form.File?.Length ?? 0, MaxBytes, Actor, cancellationToken);
        logger.LogInformation("Library document {DocumentId} updated by {ActorId}", id, Actor);
        return new(true, "سند ذخیره شد");
    }

    [HttpPost("documents/{id:long}/activate")] public async Task<OperationResult> Activate(long id)
    { await service.SetDocumentActiveAsync(id, true, Actor); logger.LogInformation("Library document {DocumentId} activated by {ActorId}", id, Actor); return new(true, "سند دوباره نمایش داده می‌شود"); }
    [HttpPost("documents/{id:long}/deactivate")] public async Task<OperationResult> Deactivate(long id)
    { await service.SetDocumentActiveAsync(id, false, Actor); logger.LogInformation("Library document {DocumentId} deactivated by {ActorId}", id, Actor); return new(true, "سند از نمایش عمومی محو شد"); }
    /// <summary>حذف دائمی سند و فایلش.</summary>
    [HttpPost("documents/{id:long}/delete")] public async Task<OperationResult> DeleteDocument(long id)
    { await service.DeleteDocumentAsync(id); logger.LogInformation("Library document {DocumentId} deleted by {ActorId}", id, Actor); return new(true, "سند حذف شد"); }
}
