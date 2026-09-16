using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OrganizationIntranet.Contracts;
using OrganizationIntranet.UI;
using OrganizationIntranet.Helper;
namespace OrganizationIntranet.Pages.Admin.Users;
[Authorize(Roles = "ADMIN")]
public class IndexModel(ApiClient api) : PageModel
{
    public List<UserDto> UserRows { get; set; } = new();
    public List<string> AllRoleNames { get; set; } = new();
    public async Task OnGetAsync()
    {
        UserRows = await api.GetAsync<List<UserDto>>("api/admin/users");
        AllRoleNames = (await api.GetAsync<List<RoleDto>>("api/admin/roles")).Where(r => r.IsActive).Select(r => r.RoleName).ToList();
    }
}
