using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OrganizationIntranet.Contracts;
using OrganizationIntranet.UI;

namespace OrganizationIntranet.Pages.Admin.AccessRequests;

[Authorize(Roles = "ADMIN")]
public class IndexModel(ApiClient api) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? Status { get; set; } = AccessRequestStatuses.Pending;
    [BindProperty(SupportsGet = true)] public int PageNumber { get; set; } = 1;
    public AccessRequestListDto Result { get; set; } = new([], 0, 1, 20);
    public string? ErrorMessage { get; set; }
    public string? SubmittedNote { get; set; }
    public long? SubmittedId { get; set; }
    private async Task LoadAsync() => Result = await api.GetAsync<AccessRequestListDto>($"api/admin/access-requests?page={PageNumber}&status={Uri.EscapeDataString(Status ?? "")}");
    public Task OnGetAsync() => LoadAsync();
    public async Task<IActionResult> OnPostReviewAsync(long requestId, Guid revision, string? note, string decision)
    {
        SubmittedId = requestId; SubmittedNote = note;
        if (!ModelState.IsValid || decision is not ("approve" or "reject"))
        { ErrorMessage = "اطلاعات تصمیم معتبر نیست"; await LoadAsync(); return Page(); }
        try
        {
            var result = await api.PostAsync<OperationResult>($"api/admin/access-requests/{requestId}/review", new ReviewAccessRequest(decision == "approve", note, revision));
            TempData["SuccessMessage"] = result.Message;
            return RedirectToPage(new { Status, PageNumber });
        }
        catch (ApiException ex) when ((int)ex.Status is 400 or 409)
        { ErrorMessage = ex.Message; await LoadAsync(); return Page(); }
    }
}
