using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OrganizationIntranet.Contracts;
using OrganizationIntranet.UI;

namespace OrganizationIntranet.Pages.Admin.Library;

/// <summary>دسته‌بندی‌های کتابخانه داخل سه بخش ثابت: ساخت، تغییر عنوان و ترتیب، فعال/غیرفعال (دسته‌ی غیرفعال سندهایش را از پورتال محو می‌کند) و حذف دسته‌ی بدون سند.</summary>
[Authorize(Policy = AdminPermissions.LibraryEditPolicy), OrganizationIntranet.Admin.Authorization.PostRequiresPermission(AdminPermissions.LibraryEdit)]
public class CategoriesModel(ApiClient api) : PageModel
{
    [BindProperty] public string? GroupCode { get; set; } = LibraryGroups.Rules;
    [BindProperty] public string? Title { get; set; }
    [BindProperty] public int DisplayOrder { get; set; }
    [BindProperty] public bool IsActive { get; set; } = true;
    public List<LibraryCategoryAdminDto> Categories { get; private set; } = [];
    public string? ErrorMessage { get; private set; }

    private async Task LoadAsync() => Categories = await api.GetAsync<List<LibraryCategoryAdminDto>>("api/admin/library/categories");
    public async Task OnGetAsync() => await LoadAsync();

    public async Task<IActionResult> OnPostCreateAsync()
    {
        try
        {
            var result = await api.PostAsync<OperationResult>("api/admin/library/categories", new LibraryCategoryRequest(GroupCode, Title, DisplayOrder, true));
            TempData["SuccessMessage"] = result.Message;
            return RedirectToPage();
        }
        catch (ApiException ex) when ((int)ex.Status is 400 or 404) { ErrorMessage = ex.Message; await LoadAsync(); return Page(); }
    }
    /// <summary>ذخیره‌ی عنوان، ترتیب و وضعیت یک دسته (بخش آن عوض نمی‌شود).</summary>
    public async Task<IActionResult> OnPostUpdateAsync(int id)
    {
        try
        {
            var result = await api.PostAsync<OperationResult>($"api/admin/library/categories/{id}", new LibraryCategoryRequest(GroupCode, Title, DisplayOrder, IsActive));
            TempData["SuccessMessage"] = result.Message;
            return RedirectToPage();
        }
        catch (ApiException ex) when ((int)ex.Status is 400 or 404) { ErrorMessage = ex.Message; await LoadAsync(); return Page(); }
    }
    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        try
        {
            var result = await api.PostAsync<OperationResult>($"api/admin/library/categories/{id}/delete");
            TempData["SuccessMessage"] = result.Message;
            return RedirectToPage();
        }
        catch (ApiException ex) when ((int)ex.Status is 400 or 404) { ErrorMessage = ex.Message; await LoadAsync(); return Page(); }
    }
}
