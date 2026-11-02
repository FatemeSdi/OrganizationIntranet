using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OrganizationIntranet.Contracts;
using OrganizationIntranet.UI;

namespace OrganizationIntranet.Pages.Admin.Organization;

/// <summary>
/// چارت سازمانی به‌شکل درخت: مشاهده با ORG_VIEW و ساخت واحد و فعال/غیرفعال‌کردن با ORG_EDIT.
/// ویرایش، جابه‌جایی و حذف در صفحه‌ی Edit است. منطق و قواعد در API (OrganizationService) اعمال می‌شود.
/// </summary>
[Authorize(Policy = AdminPermissions.OrgViewPolicy), OrganizationIntranet.Admin.Authorization.PostRequiresPermission(AdminPermissions.OrgEdit)]
public class IndexModel(ApiClient api) : PageModel
{
    [BindProperty] public string? Title { get; set; }
    [BindProperty] public int? ParentId { get; set; }
    [BindProperty] public int DisplayOrder { get; set; }
    public List<OrgUnitDto> Units { get; private set; } = [];
    public string? ErrorMessage { get; private set; }

    private async Task LoadAsync() => Units = await api.GetAsync<List<OrgUnitDto>>("api/admin/org/units");
    public async Task OnGetAsync() => await LoadAsync();

    /// <summary>ساخت واحد تازه زیر واحد انتخاب‌شده (یا ریشه).</summary>
    public async Task<IActionResult> OnPostCreateAsync()
    {
        try
        {
            var result = await api.PostAsync<OperationResult>("api/admin/org/units", new OrgUnitRequest(Title, ParentId, DisplayOrder));
            TempData["SuccessMessage"] = result.Message;
            return RedirectToPage();
        }
        catch (ApiException ex) when ((int)ex.Status is 400 or 409) { ErrorMessage = ex.Message; await LoadAsync(); return Page(); }
    }

    /// <summary>فعال/غیرفعال کردن واحد (و زیرمجموعه‌هایش هنگام غیرفعال‌شدن).</summary>
    public async Task<IActionResult> OnPostToggleAsync(int unitId)
    {
        try { await api.PostAsync<OperationResult>($"api/admin/org/units/{unitId}/toggle"); return RedirectToPage(); }
        catch (ApiException ex) when ((int)ex.Status is 400 or 404) { ErrorMessage = ex.Message; await LoadAsync(); return Page(); }
    }
}
