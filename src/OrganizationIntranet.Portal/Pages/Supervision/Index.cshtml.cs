using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OrganizationIntranet.Authorization;
using OrganizationIntranet.Contracts;
using OrganizationIntranet.UI;
namespace OrganizationIntranet.Pages.Supervision;

/// <summary>
/// کارتابل نظارت (فقط‌خواندنی): درخواست‌های دسترسی سامانه‌هایی که کاربر ناظرشان است، با تب وضعیت، فیلتر سامانه و صفحه‌بندی سمت سرور.
/// تصمیم درباره‌ی درخواست فقط در پنل مدیریت و با مجوز ACCESS_REQUEST_REVIEW است.
/// </summary>
[Authorize]
public class IndexModel(ApiClient api) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? Status { get; set; }
    [BindProperty(SupportsGet = true)] public int? App { get; set; }
    [BindProperty(SupportsGet = true)] public int PageNumber { get; set; } = 1;
    public SupervisionSummaryDto Summary { get; private set; } = new([], new());
    public AccessRequestListDto Requests { get; private set; } = new([], 0, 1, 20);
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(Requests.Total / (double)Math.Max(1, Requests.PageSize)));
    /// <summary>بررسی‌کننده‌ی درخواست‌ها در پنل مدیریت (فقط برای نمایش پیوند؛ API دوباره بررسی می‌کند).</summary>
    public bool CanReview => User.HasPermission(AdminPermissions.IntranetAdmin) && User.HasPermission(AdminPermissions.AccessRequestReview);

    public Dictionary<string, string?> Route(string? status = "", int? app = -1, int? page = null) => new()
    {
        ["status"] = status == "" ? Status : status,
        ["app"] = (app == -1 ? App : app)?.ToString(),
        ["pageNumber"] = page is > 1 ? page.ToString() : null,
    };

    public async Task OnGetAsync()
    {
        if (Status is not null && !AccessRequestStatuses.IsValid(Status)) Status = null;
        if (PageNumber < 1) PageNumber = 1;
        Summary = await api.GetAsync<SupervisionSummaryDto>("api/portal/supervision/inbox/summary");
        if (!Summary.IsSupervisor) return;
        if (App is { } id && Summary.Applications.All(a => a.ApplicationId != id)) App = null;
        if (App is not null) Summary = await api.GetAsync<SupervisionSummaryDto>($"api/portal/supervision/inbox/summary?applicationId={App}");
        Requests = await api.GetAsync<AccessRequestListDto>($"api/portal/supervision/inbox?page={PageNumber}&status={Status}&applicationId={App}");
    }
}
