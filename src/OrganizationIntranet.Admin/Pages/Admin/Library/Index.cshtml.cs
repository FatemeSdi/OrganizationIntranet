using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OrganizationIntranet.Contracts;
using OrganizationIntranet.UI;

namespace OrganizationIntranet.Pages.Admin.Library;

/// <summary>فهرست اسناد و فرم‌های کتابخانه با فیلتر بخش، دسته، وضعیت و جست‌وجو؛ فعال/غیرفعال و حذف دائمی. قواعد در API (LibraryService) اعمال می‌شود.</summary>
[Authorize(Policy = AdminPermissions.LibraryEditPolicy), OrganizationIntranet.Admin.Authorization.PostRequiresPermission(AdminPermissions.LibraryEdit)]
public class IndexModel(ApiClient api) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? Group { get; set; }
    [BindProperty(SupportsGet = true)] public int? CategoryId { get; set; }
    [BindProperty(SupportsGet = true)] public string? Status { get; set; }
    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    [BindProperty(SupportsGet = true)] public int PageNumber { get; set; } = 1;
    public LibraryDocumentPageDto Result { get; private set; } = new([], 0, 1, LibraryRules.PageSize);
    public List<LibraryCategoryAdminDto> Categories { get; private set; } = [];
    public string? ErrorMessage { get; private set; }

    private async Task LoadAsync()
    {
        Categories = await api.GetAsync<List<LibraryCategoryAdminDto>>("api/admin/library/categories");
        var query = new List<string> { $"page={Math.Max(1, PageNumber)}" };
        if (!string.IsNullOrEmpty(Group)) query.Add("group=" + Uri.EscapeDataString(Group));
        if (CategoryId is { } category) query.Add($"category={category}");
        if (!string.IsNullOrEmpty(Status)) query.Add("status=" + Uri.EscapeDataString(Status));
        if (!string.IsNullOrWhiteSpace(Search)) query.Add("search=" + Uri.EscapeDataString(Search.Trim()));
        Result = await api.GetAsync<LibraryDocumentPageDto>("api/admin/library/documents?" + string.Join("&", query));
    }
    public async Task OnGetAsync()
    {
        try { await LoadAsync(); }
        catch (ApiException ex) when ((int)ex.Status == 400) { ErrorMessage = ex.Message; Group = null; Status = null; Search = null; CategoryId = null; PageNumber = 1; await LoadAsync(); }
    }

    /// <summary>پنهان‌کردن سند از نمایش عمومی یا نمایش دوباره‌ی آن.</summary>
    public async Task<IActionResult> OnPostSetActiveAsync(long id, bool active)
    {
        try
        {
            var result = await api.PostAsync<OperationResult>($"api/admin/library/documents/{id}/{(active ? "activate" : "deactivate")}");
            TempData["SuccessMessage"] = result.Message;
        }
        catch (ApiException ex) when ((int)ex.Status is 400 or 404) { TempData["ErrorMessage"] = ex.Message; }
        return RedirectToPage(new { group = Group, categoryId = CategoryId, status = Status, search = Search, pageNumber = PageNumber });
    }
    /// <summary>حذف دائمی سند و فایل آن.</summary>
    public async Task<IActionResult> OnPostDeleteAsync(long id)
    {
        try
        {
            var result = await api.PostAsync<OperationResult>($"api/admin/library/documents/{id}/delete");
            TempData["SuccessMessage"] = result.Message;
        }
        catch (ApiException ex) when ((int)ex.Status is 400 or 404) { TempData["ErrorMessage"] = ex.Message; }
        return RedirectToPage(new { group = Group, categoryId = CategoryId, status = Status, search = Search, pageNumber = PageNumber });
    }
}
