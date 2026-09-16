using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OrganizationIntranet.Contracts;
using OrganizationIntranet.UI;
using OrganizationIntranet.Helper;

namespace OrganizationIntranet.Pages.Admin.Permissions;
[Authorize(Roles = "ADMIN")]
public class IndexModel(ApiClient api) : PageModel
{
    [BindProperty]
    public string PermissionName { get; set; } = string.Empty;

    [BindProperty]
    public string PermissionCode { get; set; } = string.Empty;

    [BindProperty]
    public int ApplicationId { get; set; }

    [BindProperty]
    public string? Description { get; set; }

    public string? ErrorMessage { get; set; }
    public List<ApplicationDto> AllApplications { get; set; } = new();
    public List<PermissionDto> AllPermissions { get; set; } = new();



    private async Task LoadAsync()
    {
        AllPermissions = await api.GetAsync<List<PermissionDto>>("api/admin/permissions");
        AllApplications = (await api.GetAsync<List<ApplicationDto>>("api/admin/applications")).Where(a => a.IsActive).ToList();
    }
    public async Task OnGetAsync() => await LoadAsync();
    public async Task<IActionResult> OnPostCreateAsync()
    {
        try
        {
            var result = await api.PostAsync<OperationResult>("api/admin/permissions", new PermissionRequest(PermissionName, PermissionCode, ApplicationId, Description));
            TempData["SuccessMessage"] = result.Message; return RedirectToPage();
        }
        catch (ApiException ex) when ((int)ex.Status is 400 or 409) { ErrorMessage = ex.Message; await LoadAsync(); return Page(); }
    }
    public async Task<IActionResult> OnPostToggleAsync(int permissionId)
    {
        await api.PostAsync<OperationResult>($"api/admin/permissions/{permissionId}/toggle");
        return RedirectToPage();
    }
}
