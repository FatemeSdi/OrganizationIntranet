using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OrganizationIntranet.Contracts;
using OrganizationIntranet.UI;
namespace OrganizationIntranet.Pages.Notifications;

/// <summary>
/// همه‌ی اعلان‌های کاربر (سامانه‌ها + هشدار درخواست دسترسی) با صفحه‌بندی، تب وضعیت، فیلتر منبع و بازه‌ی زمانی؛
/// همه‌ی فیلترها در API اعمال می‌شوند و با query string کار می‌کنند تا بدون JavaScript هم کار کند.
/// </summary>
[Authorize]
public class IndexModel(ApiClient api) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? Tab { get; set; }
    [BindProperty(SupportsGet = true)] public string? Source { get; set; }
    [BindProperty(SupportsGet = true)] public int? Days { get; set; }
    [BindProperty(SupportsGet = true)] public int PageNumber { get; set; } = 1;
    public NotificationCenterDto Center { get; private set; } = new([], 0, 1, NotificationCenter.PageSize, 0, 0);

    /// <summary>پارامترهای فعلی با جایگزینی بعضی؛ تغییر فیلتر صفحه را به ۱ برمی‌گرداند.</summary>
    public Dictionary<string, string?> Route(string? tab = "", string? source = "", int? days = -1, int? page = null) => new()
    {
        ["tab"] = tab == "" ? Tab : tab,
        ["source"] = source == "" ? Source : source,
        ["days"] = (days == -1 ? Days : days)?.ToString(),
        ["pageNumber"] = page is > 1 ? page.ToString() : null,
    };

    public async Task OnGetAsync()
    {
        // مقدار نامعتبر از query string به پیش‌فرض برمی‌گردد (API آن را رد می‌کند).
        if (!NotificationCenter.ValidStatus(Tab)) Tab = null;
        if (!NotificationCenter.ValidKind(Source)) Source = null;
        if (!NotificationCenter.ValidDays(Days)) Days = null;
        if (PageNumber < 1) PageNumber = 1;
        Center = await api.GetAsync<NotificationCenterDto>($"api/portal/notifications/page?kind={Source}&status={Tab}&days={Days}&page={PageNumber}");
        if (Center.Items.Count == 0 && PageNumber > Center.TotalPages)
        {
            PageNumber = Center.TotalPages; // مثلاً پس از «خواندن همه» در تب خوانده‌نشده
            Center = await api.GetAsync<NotificationCenterDto>($"api/portal/notifications/page?kind={Source}&status={Tab}&days={Days}&page={PageNumber}");
        }
    }

    public async Task<IActionResult> OnPostOpenAsync(long id)
    {
        try { return Redirect((await api.PostAsync<NotificationDestinationDto>($"api/portal/notifications/{id}/open")).Url); }
        catch (ApiException ex) when ((int)ex.Status is 400 or 404) { TempData["NotificationsMessage"] = ex.Message; return RedirectToPage(); }
    }

    public async Task<IActionResult> OnPostReadAlertAsync(long id)
    {
        await api.PostAsync<OperationResult>($"api/portal/supervision/alerts/{id}/read");
        return Redirect("/Supervision?status=" + AccessRequestStatuses.Pending);
    }

    public async Task<IActionResult> OnPostReadAllAsync()
    {
        await api.PostAsync<OperationResult>("api/portal/notifications/read");
        await api.PostAsync<OperationResult>("api/portal/supervision/alerts/read");
        TempData["NotificationsMessage"] = "همه‌ی اعلان‌ها خوانده شد";
        return RedirectToPage(Route());
    }
}
