using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OrganizationIntranet.Contracts;
using OrganizationIntranet.UI;
using OrganizationIntranet.Helper;

namespace OrganizationIntranet.Pages.Admin.Applications;
[Authorize(Roles = "ADMIN")]
public class IndexModel(ApiClient api) : PageModel
{
    [BindProperty]
    public string ApplicationName { get; set; } = string.Empty;

    [BindProperty]
    public string ApplicationCode { get; set; } = string.Empty;

    [BindProperty]
    public string? BaseUrl { get; set; }

    [BindProperty]
    public string? Description { get; set; }

    [BindProperty]
    public int DisplayOrder { get; set; }

    public string? ErrorMessage { get; set; }
    public List<ApplicationDto> AllApplications { get; set; } = new();



    private async Task LoadAsync() { AllApplications = await api.GetAsync<List<ApplicationDto>>("api/admin/applications"); }
    public async Task OnGetAsync() => await LoadAsync();
    public async Task<IActionResult> OnPostCreateAsync()
    {
        try
        {
            var result = await api.PostAsync<OperationResult>("api/admin/applications", new ApplicationRequest(ApplicationName, ApplicationCode, BaseUrl, Description, DisplayOrder));
            TempData["SuccessMessage"] = result.Message; return RedirectToPage();
        }
        catch (ApiException ex) when ((int)ex.Status is 400 or 409) { ErrorMessage = ex.Message; await LoadAsync(); return Page(); }
    }
    public async Task<IActionResult> OnPostToggleAsync(int applicationId)
    {
        await api.PostAsync<OperationResult>($"api/admin/applications/{applicationId}/toggle");
        return RedirectToPage();
    }
}
