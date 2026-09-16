using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OrganizationIntranet.Contracts;
using OrganizationIntranet.UI;
using OrganizationIntranet.Helper;

namespace OrganizationIntranet.Pages.Admin.Permissions;
[Authorize(Roles = "ADMIN")]
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
