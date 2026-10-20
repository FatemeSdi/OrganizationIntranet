using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OrganizationIntranet.Contracts;
using OrganizationIntranet.UI;
using OrganizationIntranet.Helper;
namespace OrganizationIntranet.Pages.Portal;
// صفحه‌ی اصلی برای ناشناس باز است (UiHost) و فقط داده‌ی عمومی نشان می‌دهد؛ همه‌ی عملیات POST نیاز به ورود دارند و API هم دوباره بررسی می‌کند.
[AllowAnonymous]
public class IndexModel(ApiClient api) : PageModel
{
    public bool IsAuthenticated => User.Identity?.IsAuthenticated == true;
    public string DisplayName { get; set; } = "";
    public string RoleNames { get; set; } = "";
    public string Initials { get; set; } = "";
    public PublicationListDto Publications { get; set; } = new([], 0, 1, 20);
    public List<PortalApplicationDto> Applications { get; set; } = new();
    public List<PortalNotificationDto> Notifications { get; set; } = new();
    public List<RequestableApplicationDto> RequestableApplications { get; set; } = new();
    public AccessRequestListDto AccessRequests { get; set; } = new([], 0, 1, 20);
    [BindProperty(SupportsGet = true)] public int RequestPage { get; set; } = 1;
    [BindProperty] public int RequestedApplicationId { get; set; }
    [BindProperty] public string? RequestReason { get; set; }
    public string? RequestError { get; set; }
    [BindProperty(SupportsGet = true)] public int SupervisionPage { get; set; } = 1;
    public AccessRequestAlertsDto SupervisionAlerts { get; set; } = new([], 0);
    public AccessRequestListDto SupervisionInbox { get; set; } = new([], 0, 1, 20);
    public int UnreadTotal => Applications.Sum(a => a.UnreadCount) + SupervisionAlerts.UnreadCount;
    private IActionResult? RequireSignIn() => IsAuthenticated ? null : Challenge();
    public async Task<IActionResult> OnPostReadSupervisionAlertAsync(long alertId)
    {
        if (RequireSignIn() is { } denied) return denied;
        await api.PostAsync<OperationResult>($"api/portal/supervision/alerts/{alertId}/read");
        return RedirectToPage(pageName: null, pageHandler: null, routeValues: null, fragment: "supervision");
    }
    public async Task<IActionResult> OnPostOpenNotificationAsync(long notificationId)
    {
        if (RequireSignIn() is { } denied) return denied;
        if (!ModelState.IsValid) return BadRequest();
        var destination = await api.PostAsync<NotificationDestinationDto>($"api/portal/notifications/{notificationId}/open");
        return Redirect(destination.Url);
    }
    public async Task<IActionResult> OnPostRequestAccessAsync()
    {
        if (RequireSignIn() is { } denied) return denied;
        if (!ModelState.IsValid) { RequestError = "سامانه و دلیل درخواست را بررسی کنید."; await OnGetAsync(); return Page(); }
        try
        {
            var result = await api.PostAsync<OperationResult>("api/portal/access-requests", new CreateAccessRequest(RequestedApplicationId, RequestReason ?? ""));
            TempData["AccessRequestMessage"] = result.Message;
            return RedirectToPage(pageName: null, pageHandler: null, routeValues: null, fragment: "access-requests");
        }
        catch (ApiException ex) when ((int)ex.Status is 400 or 409)
        { RequestError = ex.Message; await OnGetAsync(); return Page(); }
    }
    public async Task<IActionResult> OnPostCancelRequestAsync(long requestId, Guid revision)
    {
        if (RequireSignIn() is { } denied) return denied;
        if (!ModelState.IsValid) return BadRequest();
        try
        {
            var result = await api.PostAsync<OperationResult>($"api/portal/access-requests/{requestId}/cancel", new CancelAccessRequest(revision));
            TempData["AccessRequestMessage"] = result.Message;
            return RedirectToPage(pageName: null, pageHandler: null, routeValues: null, fragment: "access-requests");
        }
        catch (ApiException ex) when ((int)ex.Status is 400 or 409)
        { RequestError = ex.Message; await OnGetAsync(); return Page(); }
    }
    public async Task<IActionResult> OnPostReadNotificationsAsync()
    {
        if (RequireSignIn() is { } denied) return denied;
        await api.PostAsync<OperationResult>("api/portal/notifications/read");
        await api.PostAsync<OperationResult>("api/portal/supervision/alerts/read");
        return RedirectToPage();
    }
    public async Task OnGetAsync()
    {
        if (!IsAuthenticated)
        {
            // بازدیدکننده‌ی ناشناس: فقط سامانه‌ها و مطالب عمومی. شمارنده و دسترسی اختصاصی ندارد.
            var publicApps = await api.GetAsync<List<PublicApplicationDto>>("api/public/applications");
            Applications = publicApps.Select(a => new PortalApplicationDto(a.ApplicationId, a.Name, a.Description, a.Url, a.Icon,
                a.RequiresLogin, a.IsInternetAccessible, true, 0, new List<string>())).ToList();
            Publications = await api.GetAsync<PublicationListDto>("api/public/publications");
            return;
        }
        var p = await api.GetAsync<ProfileDto>("api/account/me");
        DisplayName = p.DisplayName; RoleNames = p.RoleNames; Initials = p.Initials;
        Applications = await api.GetAsync<List<PortalApplicationDto>>("api/portal/applications");
        Notifications = await api.GetAsync<List<PortalNotificationDto>>("api/portal/notifications");
        SupervisionAlerts = await api.GetAsync<AccessRequestAlertsDto>("api/portal/supervision/alerts");
        SupervisionInbox = await api.GetAsync<AccessRequestListDto>($"api/portal/supervision/inbox?page={SupervisionPage}");
        RequestableApplications = await api.GetAsync<List<RequestableApplicationDto>>("api/portal/access-requests/applications");
        AccessRequests = await api.GetAsync<AccessRequestListDto>($"api/portal/access-requests?page={RequestPage}");
        Publications = await api.GetAsync<PublicationListDto>("api/publications");
    }
}
