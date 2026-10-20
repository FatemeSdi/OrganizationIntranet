using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OrganizationIntranet.Contracts;
using OrganizationIntranet.UI;

namespace OrganizationIntranet.Pages.Admin.Applications;

[Authorize(Roles = "ADMIN")]
public class ImportModel(ApiClient api) : PageModel
{
    public CatalogImportPreviewDto? Preview { get; set; }
    public string? ErrorMessage { get; set; }
    public async Task OnGetAsync()
    {
        try { Preview = await api.GetAsync<CatalogImportPreviewDto>("api/admin/catalog-import"); }
        catch (ApiException ex) when ((int)ex.Status is 400 or 409) { ErrorMessage = ex.Message; }
    }
    public async Task<IActionResult> OnPostAsync()
    {
        try
        {
            var result = await api.PostAsync<CatalogImportResultDto>("api/admin/catalog-import");
            TempData["SuccessMessage"] = $"{result.Created} سامانه ثبت شد؛ {result.Existing} سامانه از قبل موجود بود. دسترسی‌های مشخص‌شده اعمال شدند.";
            return RedirectToPage("Index");
        }
        catch (ApiException ex) when ((int)ex.Status is 400 or 409) { await OnGetAsync(); ErrorMessage = ex.Message; return Page(); }
    }
}
