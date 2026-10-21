using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OrganizationIntranet.Contracts;
using OrganizationIntranet.UI;
namespace OrganizationIntranet.Pages.Notifications;

/// <summary>یک اعلان در فهرست یکپارچه: اعلان سامانه‌ها یا هشدار درخواست دسترسی (کارتابل نظارت).</summary>
public sealed record NotificationRow(string Kind, long Id, string Source, string Title, DateTime CreatedAt, bool Unread, bool HasDestination);

/// <summary>همه‌ی اعلان‌های کاربر با تب خوانده‌نشده/خوانده‌شده و فیلتر منبع؛ تب‌ها با query string کار می‌کنند تا بدون JavaScript هم کار کند.</summary>
[Authorize]
public class IndexModel(ApiClient api) : PageModel
{
    public const string SystemKind = "system", RequestKind = "request";
    [BindProperty(SupportsGet = true)] public string? Tab { get; set; }
    [BindProperty(SupportsGet = true)] public string? Source { get; set; }
    public List<NotificationRow> All { get; private set; } = new();
    public IEnumerable<NotificationRow> Visible => All
        .Where(n => Source is null || n.Kind == Source)
        .Where(n => Tab switch { "unread" => n.Unread, "read" => !n.Unread, _ => true });
    public int Count(string? tab) => All.Where(n => Source is null || n.Kind == Source)
        .Count(n => tab switch { "unread" => n.Unread, "read" => !n.Unread, _ => true });

    public async Task OnGetAsync()
    {
        if (Tab is not ("unread" or "read")) Tab = null;
        if (Source is not (SystemKind or RequestKind)) Source = null;
        var notifications = await api.GetAsync<List<PortalNotificationDto>>("api/portal/notifications");
        var alerts = await api.GetAsync<AccessRequestAlertsDto>("api/portal/supervision/alerts");
        All = notifications.Select(n => new NotificationRow(SystemKind, n.Id, n.System, n.Title, n.CreatedAt, n.Unread, n.TargetUrl is not null))
            .Concat(alerts.Items.Select(a => new NotificationRow(RequestKind, a.Id, a.ApplicationName, $"درخواست دسترسی جدید از {a.Requester}", a.CreatedAt, !a.IsRead, true)))
            .OrderByDescending(n => n.CreatedAt).ToList();
    }

    public async Task<IActionResult> OnPostOpenAsync(long id)
    {
        try { return Redirect((await api.PostAsync<NotificationDestinationDto>($"api/portal/notifications/{id}/open")).Url); }
        catch (ApiException ex) when ((int)ex.Status is 400 or 404) { TempData["NotificationsMessage"] = ex.Message; return RedirectToPage(); }
    }

    public async Task<IActionResult> OnPostReadAlertAsync(long id)
    {
        await api.PostAsync<OperationResult>($"api/portal/supervision/alerts/{id}/read");
        return Redirect("/Portal#supervision");
    }

    public async Task<IActionResult> OnPostReadAllAsync()
    {
        await api.PostAsync<OperationResult>("api/portal/notifications/read");
        await api.PostAsync<OperationResult>("api/portal/supervision/alerts/read");
        TempData["NotificationsMessage"] = "همه‌ی اعلان‌ها خوانده شد";
        return RedirectToPage(new { Tab, Source });
    }
}
