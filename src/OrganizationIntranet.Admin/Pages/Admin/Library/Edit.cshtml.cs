using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OrganizationIntranet.Contracts;
using OrganizationIntranet.UI;

namespace OrganizationIntranet.Pages.Admin.Library;

/// <summary>
/// سند جدید (یک یا چند فایل) یا ویرایش یک سند. در حالت «سند جدید» می‌توان چند فایل با هم انتخاب کرد: عنوان هر سند از نام فایلش گرفته می‌شود و هر فایل مستقل ثبت یا رد می‌شود؛
/// فقط اگر یک فایل انتخاب شود و عنوان نوشته شود همان عنوان استفاده می‌شود. در ویرایش فایل اختیاری است (خالی = فایل فعلی). نوع و امضای فایل در API بررسی می‌شود.
/// </summary>
[Authorize(Policy = AdminPermissions.LibraryEditPolicy), OrganizationIntranet.Admin.Authorization.PostRequiresPermission(AdminPermissions.LibraryEdit)]
[RequestSizeLimit(LibraryRules.MaxRequestBytes), RequestFormLimits(MultipartBodyLengthLimit = LibraryRules.MaxRequestBytes)]
public class EditModel(ApiClient api, IConfiguration configuration) : PageModel
{
    [BindProperty(SupportsGet = true)] public long? Id { get; set; }
    [BindProperty] public int CategoryId { get; set; }
    [BindProperty] public string? Title { get; set; }
    [BindProperty] public string? Description { get; set; }
    [BindProperty] public bool IsActive { get; set; } = true;
    [BindProperty] public IFormFile? File { get; set; }
    /// <summary>فایل‌های انتخاب‌شده در حالت «سند جدید» (چندتایی).</summary>
    [BindProperty] public List<IFormFile> Files { get; set; } = [];
    public LibraryDocumentAdminDto? Existing { get; private set; }
    public List<LibraryCategoryAdminDto> Categories { get; private set; } = [];
    public string? ErrorMessage { get; private set; }
    public bool IsNew => Id is null or 0;
    /// <summary>سقف حجم فایل برای راهنمای فرم (همان تنظیم API؛ فقط راهنماست، API خودش اعمال می‌کند).</summary>
    public long MaxBytes => configuration.GetValue<long?>("Library:MaxFileBytes") is > 0 and var configured ? configured : LibraryRules.DefaultMaxFileBytes;

    private async Task LoadAsync()
    {
        Categories = (await api.GetAsync<List<LibraryCategoryAdminDto>>("api/admin/library/categories")).Where(c => c.IsActive || c.CategoryId == CategoryId).ToList();
        if (!IsNew) Existing = await api.GetAsync<LibraryDocumentAdminDto>($"api/admin/library/documents/{Id}");
    }
    public async Task<IActionResult> OnGetAsync()
    {
        try { await LoadAsync(); }
        catch (ApiException ex) when ((int)ex.Status == 404) { return NotFound(); }
        if (Existing is { } d) { CategoryId = d.CategoryId; Title = d.Title; Description = d.Description; IsActive = d.IsActive; }
        else if (Categories.Count > 0) CategoryId = Categories[0].CategoryId;
        return Page();
    }
    /// <summary>دانلود فایل فعلی سند برای مدیر (حتی وقتی غیرفعال است).</summary>
    public async Task<IActionResult> OnGetDownloadAsync()
    {
        if (IsNew) return NotFound();
        try
        {
            var file = await api.GetFileAsync($"api/admin/library/documents/{Id}/file");
            return File(file.Content, file.ContentType, file.FileName ?? "document");
        }
        catch (ApiException ex) when ((int)ex.Status == 404) { return NotFound(); }
    }
    /// <summary>پیش‌نمایش درون‌خطی تصویر سند برای مدیر (حتی وقتی غیرفعال است).</summary>
    public async Task<IActionResult> OnGetPreviewAsync()
    {
        if (IsNew) return NotFound();
        try
        {
            var file = await api.GetFileAsync($"api/admin/library/documents/{Id}/preview");
            Response.Headers["X-Content-Type-Options"] = "nosniff";
            return File(file.Content, file.ContentType);
        }
        catch (ApiException ex) when ((int)ex.Status == 404) { return NotFound(); }
    }

    /// <summary>سند جدید: یک فایل با عنوان دلخواه، یا چند فایل (عنوان از نام فایل) از راه آپلود چندتایی.</summary>
    private async Task<IActionResult> CreateAsync()
    {
        var files = Files.Where(f => f.Length > 0).ToList();
        if (files.Count == 0) { ErrorMessage = "دست‌کم یک فایل انتخاب کنید"; return await ReloadAsync(); }
        if (files.Count > LibraryRules.MaxBatchFiles) { ErrorMessage = $"در هر آپلود حداکثر {LibraryRules.MaxBatchFiles} فایل مجاز است"; return await ReloadAsync(); }
        try
        {
            if (files.Count == 1 && !string.IsNullOrWhiteSpace(Title))
            {
                File = files[0];
                return await SaveSingleAsync();
            }
            using var form = new MultipartFormDataContent
            {
                { new StringContent(CategoryId.ToString()), "CategoryId" }, { new StringContent(Description ?? ""), "Description" },
                { new StringContent(IsActive ? "true" : "false"), "IsActive" },
            };
            var streams = new List<Stream>();
            try
            {
                foreach (var file in files) { var stream = file.OpenReadStream(); streams.Add(stream); form.Add(new StreamContent(stream), "Files", file.FileName); }
                var result = await api.PostFormAsync<LibraryBatchResultDto>("api/admin/library/documents/batch", form);
                var failures = string.Join(" ؛ ", result.Failures.Take(10).Select(f => $"{f.FileName}: {f.Reason}")) + (result.Failures.Count > 10 ? $" ؛ و {result.Failures.Count - 10} مورد دیگر" : "");
                if (result.Created == 0) { ErrorMessage = "هیچ سندی ثبت نشد. " + failures; return await ReloadAsync(); }
                TempData["SuccessMessage"] = $"{result.Created} سند ثبت شد";
                if (result.Failures.Count > 0) TempData["ErrorMessage"] = $"{result.Failures.Count} فایل رد شد. " + failures;
                return RedirectToPage("Index");
            }
            finally { foreach (var stream in streams) await stream.DisposeAsync(); }
        }
        catch (ApiException ex) when ((int)ex.Status is 400 or 404 or 413) { ErrorMessage = ex.Message; return await ReloadAsync(); }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (IsNew) return await CreateAsync();
        return await SaveSingleAsync();
    }

    /// <summary>ساخت (با یک فایل) یا ویرایش یک سند.</summary>
    private async Task<IActionResult> SaveSingleAsync()
    {
        if (File is { Length: > 0 } && File.Length > MaxBytes) { ErrorMessage = $"حجم فایل باید حداکثر {MaxBytes / (1024 * 1024)} مگابایت باشد"; return await ReloadAsync(); }
        using var form = new MultipartFormDataContent
        {
            { new StringContent(CategoryId.ToString()), nameof(LibraryDocumentForm.CategoryId) },
            { new StringContent(Title ?? ""), nameof(LibraryDocumentForm.Title) },
            { new StringContent(Description ?? ""), nameof(LibraryDocumentForm.Description) },
            { new StringContent(IsActive ? "true" : "false"), nameof(LibraryDocumentForm.IsActive) },
        };
        await using var stream = File is { Length: > 0 } ? File.OpenReadStream() : null;
        if (stream is not null) form.Add(new StreamContent(stream), nameof(LibraryDocumentForm.File), File!.FileName);
        try
        {
            var result = await api.PostFormAsync<OperationResult>(IsNew ? "api/admin/library/documents" : $"api/admin/library/documents/{Id}", form);
            TempData["SuccessMessage"] = result.Message;
            return RedirectToPage("Index");
        }
        catch (ApiException ex) when ((int)ex.Status is 400 or 404 or 413) { ErrorMessage = ex.Message; return await ReloadAsync(); }
    }
    private async Task<IActionResult> ReloadAsync()
    {
        try { await LoadAsync(); } catch (ApiException ex) when ((int)ex.Status == 404) { return NotFound(); }
        return Page();
    }

    /// <summary>فیلدهای فرم چندبخشی؛ همان نام‌هایی که API انتظار دارد.</summary>
    private static class LibraryDocumentForm { public const string CategoryId = "CategoryId", Title = "Title", Description = "Description", IsActive = "IsActive", File = "File"; }
}
