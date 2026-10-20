using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OrganizationIntranet.Contracts;
using OrganizationIntranet.UI;

namespace OrganizationIntranet.Pages.Admin.AccessRequests;

[Authorize(Roles = "ADMIN")]
public class NotificationsModel(ApiClient api) : PageModel
{
    [BindProperty(SupportsGet = true)] public int PageNumber { get; set; } = 1;
    public AccessRequestSmsListDto Result { get; set; } = new([], 0, 1, 20);
    public string? ErrorMessage { get; set; }
    public async Task OnGetAsync() => Result = await api.GetAsync<AccessRequestSmsListDto>($"api/admin/supervision/sms?page={PageNumber}");
    public async Task<IActionResult> OnPostReadAsync(long alertId)
    { await api.PostAsync<OperationResult>($"api/admin/supervision/alerts/{alertId}/read"); return RedirectToPage("Index", new { Status = "" }); }
    public async Task<IActionResult> OnPostRetryAsync(long alertId)
    {
        try { var result = await api.PostAsync<OperationResult>($"api/admin/supervision/sms/{alertId}/retry"); TempData["SuccessMessage"] = result.Message; return RedirectToPage(new { PageNumber }); }
        catch (ApiException ex) when ((int)ex.Status is 400 or 409) { ErrorMessage = ex.Message; await OnGetAsync(); return Page(); }
    }
}
