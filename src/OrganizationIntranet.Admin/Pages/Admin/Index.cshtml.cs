using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OrganizationIntranet.Contracts;
using OrganizationIntranet.UI;
using OrganizationIntranet.Helper;

namespace OrganizationIntranet.Pages.Admin;
[Authorize(Roles = "ADMIN")]
public class IndexModel(ApiClient api) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public bool Welcome { get; set; }

    public int TotalUsers { get; set; }
    public int ActiveUsers { get; set; }
    public int TotalRoles { get; set; }
    public int TotalApplications { get; set; }
    public List<UserDto> RecentUsers { get; set; } = new();
    public string AdminDisplayName { get; set; } = string.Empty;
    public string TodayPersian { get; set; } = string.Empty;



    public async Task OnGetAsync()
    {
        AdminDisplayName = $"{User.FindFirstValue(ClaimTypes.GivenName)} {User.FindFirstValue(ClaimTypes.Surname)}".Trim();
        TodayPersian = DateTime.Now.ToPersianDate();
        var data = await api.GetAsync<DashboardDto>("api/admin/dashboard");
        TotalUsers = data.TotalUsers; ActiveUsers = data.ActiveUsers; TotalRoles = data.TotalRoles; TotalApplications = data.TotalApplications; RecentUsers = data.RecentUsers;
    }
}
