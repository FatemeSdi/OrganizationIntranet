using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OrganizationIntranet.Contracts;
using OrganizationIntranet.UI;
using OrganizationIntranet.Helper;

namespace OrganizationIntranet.Pages.Admin.Roles;
[Authorize(Roles = "ADMIN")]
public class IndexModel(ApiClient api) : PageModel
{
    [BindProperty]
    public string RoleName { get; set; } = string.Empty;

    [BindProperty]
    public string RoleCode { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }
    public List<RoleDto> AllRoles { get; set; } = new();



    private async Task LoadAsync() { AllRoles = await api.GetAsync<List<RoleDto>>("api/admin/roles"); }
    public async Task OnGetAsync() => await LoadAsync();
    public async Task<IActionResult> OnPostCreateAsync()
    {
        try
        {
            var result = await api.PostAsync<OperationResult>("api/admin/roles", new RoleRequest(RoleName, RoleCode));
            TempData["SuccessMessage"] = result.Message; return RedirectToPage();
        }
        catch (ApiException ex) when ((int)ex.Status is 400 or 409) { ErrorMessage = ex.Message; await LoadAsync(); return Page(); }
    }
    public async Task<IActionResult> OnPostToggleAsync(int roleId)
    {
        await api.PostAsync<OperationResult>($"api/admin/roles/{roleId}/toggle");
        return RedirectToPage();
    }
}
