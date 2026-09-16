using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OrganizationIntranet.Contracts;
using OrganizationIntranet.UI;
using OrganizationIntranet.Helper;
namespace OrganizationIntranet.Pages.Portal;
[Authorize]
public class IndexModel(ApiClient api) : PageModel
{
    public string DisplayName { get; set; } = "";
    public string RoleNames { get; set; } = "";
    public string Initials { get; set; } = "";
    public PublicationListDto Publications { get; set; } = new([], 0, 1, 20);
    public async Task OnGetAsync()
    {
        var p = await api.GetAsync<ProfileDto>("api/account/me");
        DisplayName = p.DisplayName; RoleNames = p.RoleNames; Initials = p.Initials;
        Publications = await api.GetAsync<PublicationListDto>("api/publications");
    }
}
