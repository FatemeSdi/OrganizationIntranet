using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OrganizationIntranet.Contracts;
using OrganizationIntranet.UI;
using OrganizationIntranet.Helper;
using OrganizationIntranet.Authorization;

namespace OrganizationIntranet.Pages.Admin;
[Authorize(Policy = OrganizationIntranet.Contracts.AdminPermissions.IntranetAdminPolicy)]
public class IndexModel(ApiClient api) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public bool Welcome { get; set; }

    public int TotalUsers { get; set; }
    public int ActiveUsers { get; set; }
    public int TotalRoles { get; set; }
    public AccessRequestListDto PendingRequests { get; set; } = new([], 0, 1, 20);
    public int TotalApplications { get; set; }
    public List<UserDto> RecentUsers { get; set; } = new();
    public string AdminDisplayName { get; set; } = string.Empty;
    public string TodayPersian { get; set; } = string.Empty;



    public async Task OnGetAsync()
    {
        AdminDisplayName = $"{User.FindFirstValue(ClaimTypes.GivenName)} {User.FindFirstValue(ClaimTypes.Surname)}".Trim();
        TodayPersian = DateTime.Now.ToPersianDate();
        // هر بخش فقط با مجوز خودش بارگذاری می‌شود
        if (User.HasPermission(AdminPermissions.AccessRequestReview))
            PendingRequests = await api.GetAsync<AccessRequestListDto>("api/admin/access-requests?status=Pending");
        var data = await api.GetAsync<DashboardDto>("api/admin/dashboard");
        TotalUsers = data.TotalUsers; ActiveUsers = data.ActiveUsers; TotalRoles = data.TotalRoles; TotalApplications = data.TotalApplications; RecentUsers = data.RecentUsers;
    }
}
