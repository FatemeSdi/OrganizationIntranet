using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OrganizationIntranet.Contracts;
using OrganizationIntranet.UI;
using OrganizationIntranet.Helper;

namespace OrganizationIntranet.Pages.Admin.Permissions;
[Authorize(Policy = OrganizationIntranet.Contracts.AdminPermissions.PermissionViewPolicy), OrganizationIntranet.Admin.Authorization.PostRequiresPermission(OrganizationIntranet.Contracts.AdminPermissions.PermissionEdit)]
public class AssignModel(ApiClient api) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public int Id { get; set; }

    [BindProperty]
    public List<int> SelectedRoleIds { get; set; } = new();

    public PermissionDto? Permission { get; set; }
    public List<RoleDto> AllRoles { get; set; } = new();



    public string? ErrorMessage { get; set; }
    private async Task LoadAsync()
    {
        Permission = await api.GetAsync<PermissionDto>($"api/admin/permissions/{Id}");
        AllRoles = (await api.GetAsync<List<RoleDto>>("api/admin/roles")).Where(r => r.IsActive).ToList();
    }
    public async Task<IActionResult> OnGetAsync()
    {
        await LoadAsync(); SelectedRoleIds = Permission!.SelectedRoleIds; return Page();
    }
    /// <summary>ثبت لحظه‌ای افزودن/برداشتن یک نقش از این مجوز (از admin-pickers.js)؛ قواعد «مجوز سیستمی از ADMIN گرفته نشود» در API اعمال می‌شود.</summary>
    public async Task<IActionResult> OnPostRoleAsync([FromBody] RoleToggleBody body)
    {
        try
        {
            var current = await api.GetAsync<PermissionDto>($"api/admin/permissions/{Id}");
            var roles = current.SelectedRoleIds.ToHashSet();
            var roleId = checked((int)body.Id);
            if (body.Granted) roles.Add(roleId); else roles.Remove(roleId);
            var result = await api.PostAsync<OperationResult>($"api/admin/permissions/{Id}/roles", new RolesRequest(roles.ToList()));
            return new JsonResult(new { ok = true, message = result.Message });
        }
        catch (ApiException ex) when ((int)ex.Status is 400 or 404 or 409) { return new JsonResult(new { ok = false, message = ex.Message }) { StatusCode = 400 }; }
    }
    public sealed record RoleToggleBody(string Kind, long Id, bool Granted);

    public async Task<IActionResult> OnPostAsync()
    {
        try
        {
            var result = await api.PostAsync<OperationResult>($"api/admin/permissions/{Id}/roles", new RolesRequest(SelectedRoleIds));
            TempData["SuccessMessage"] = result.Message; return RedirectToPage("/Admin/Permissions/Index");
        }
        catch (ApiException ex) when ((int)ex.Status is 400 or 409) { ErrorMessage = ex.Message; await LoadAsync(); return Page(); }
    }
}
