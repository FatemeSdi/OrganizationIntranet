using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OrganizationIntranet.Contracts;
using OrganizationIntranet.UI;
namespace OrganizationIntranet.Pages.AccessRequests;

/// <summary>صفحه‌ی «درخواست دسترسی جدید» (از منوی حساب): ثبت درخواست و پیگیری/لغو درخواست‌های من. بررسی نهایی در API است.</summary>
[Authorize]
public class IndexModel(ApiClient api) : PageModel
{
    [BindProperty(SupportsGet = true)] public int RequestPage { get; set; } = 1;
    [BindProperty(SupportsGet = true)] public string? Status { get; set; }
    [BindProperty, Range(1, int.MaxValue, ErrorMessage = "سامانه را انتخاب کنید")] public int RequestedApplicationId { get; set; }
    [BindProperty, Required(ErrorMessage = "دلیل نیاز به دسترسی را بنویسید"), StringLength(1000)] public string? RequestReason { get; set; }
    public List<RequestableApplicationDto> RequestableApplications { get; set; } = new();
    public AccessRequestListDto AccessRequests { get; set; } = new([], 0, 1, 20);
    // فیلتر وضعیت و صفحه‌بندی هر دو در API انجام می‌شوند (نه فقط روی صفحه‌ی جاری).
    public IEnumerable<AccessRequestDto> Visible => AccessRequests.Items;
    public string? Error { get; set; }

    private async Task LoadAsync()
    {
        if (RequestPage < 1) RequestPage = 1;
        if (Status is not (AccessRequestStatuses.Pending or AccessRequestStatuses.Approved or AccessRequestStatuses.Rejected or AccessRequestStatuses.Cancelled)) Status = null;
        RequestableApplications = await api.GetAsync<List<RequestableApplicationDto>>("api/portal/access-requests/applications");
        AccessRequests = await api.GetAsync<AccessRequestListDto>($"api/portal/access-requests?page={RequestPage}&status={Status}");
    }
    public async Task OnGetAsync() => await LoadAsync();

    public async Task<IActionResult> OnPostRequestAccessAsync()
    {
        if (!ModelState.IsValid) { Error = "سامانه و دلیل درخواست را بررسی کنید."; await LoadAsync(); return Page(); }
        try
        {
            var result = await api.PostAsync<OperationResult>("api/portal/access-requests", new CreateAccessRequest(RequestedApplicationId, RequestReason ?? ""));
            TempData["AccessRequestMessage"] = result.Message;
            return RedirectToPage();
        }
        catch (ApiException ex) when ((int)ex.Status is 400 or 409) { Error = ex.Message; await LoadAsync(); return Page(); }
    }

    public async Task<IActionResult> OnPostCancelRequestAsync(long requestId, Guid revision)
    {
        ModelState.Clear();
        try
        {
            var result = await api.PostAsync<OperationResult>($"api/portal/access-requests/{requestId}/cancel", new CancelAccessRequest(revision));
            TempData["AccessRequestMessage"] = result.Message;
            return RedirectToPage(new { RequestPage, Status });
        }
        catch (ApiException ex) when ((int)ex.Status is 400 or 409) { Error = ex.Message; await LoadAsync(); return Page(); }
    }
}
