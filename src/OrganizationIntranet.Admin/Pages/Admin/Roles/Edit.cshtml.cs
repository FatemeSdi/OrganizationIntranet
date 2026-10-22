using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OrganizationIntranet.Contracts;
using OrganizationIntranet.UI;

namespace OrganizationIntranet.Pages.Admin.Roles;

/// <summary>جزئیات نقش: ویرایش نام/کد، اعضا (با USER_VIEW)، مجوزها و حذف نقش استفاده‌نشده.</summary>
[Authorize(Policy = AdminPermissions.RoleViewPolicy), OrganizationIntranet.Admin.Authorization.PostRequiresPermission(AdminPermissions.RoleEdit)]
public class EditModel(ApiClient api) : PageModel
{
    [BindProperty(SupportsGet = true)] public int Id { get; set; }
    [BindProperty] public string RoleName { get; set; } = string.Empty;
    [BindProperty] public string RoleCode { get; set; } = string.Empty;
    public RoleDetailsDto Details { get; private set; } = null!;
    public string? ErrorMessage { get; set; }

    private async Task<bool> LoadAsync()
    {
        try { Details = await api.GetAsync<RoleDetailsDto>($"api/admin/roles/{Id}"); return true; }
        catch (ApiException ex) when ((int)ex.Status == 404) { return false; }
    }
    public async Task<IActionResult> OnGetAsync()
    {
        if (!await LoadAsync()) return NotFound();
        RoleName = Details.Role.RoleName; RoleCode = Details.Role.RoleCode;
        return Page();
    }
    public async Task<IActionResult> OnPostAsync()
    {
        try
        {
            var result = await api.PostAsync<OperationResult>($"api/admin/roles/{Id}", new RoleRequest(RoleName, RoleCode));
            TempData["SuccessMessage"] = result.Message;
            return RedirectToPage("/Admin/Roles/Index");
        }
        catch (ApiException ex) when ((int)ex.Status is 400 or 409) { ErrorMessage = ex.Message; return await LoadAsync() ? Page() : NotFound(); }
    }
    public async Task<IActionResult> OnPostDeleteAsync()
    {
        try
        {
            var result = await api.PostAsync<OperationResult>($"api/admin/roles/{Id}/delete");
            TempData["SuccessMessage"] = result.Message;
            return RedirectToPage("/Admin/Roles/Index");
        }
        catch (ApiException ex) when ((int)ex.Status is 400 or 409)
        {
            ErrorMessage = ex.Message;
            if (!await LoadAsync()) return NotFound();
            RoleName = Details.Role.RoleName; RoleCode = Details.Role.RoleCode;
            return Page();
        }
    }
}
