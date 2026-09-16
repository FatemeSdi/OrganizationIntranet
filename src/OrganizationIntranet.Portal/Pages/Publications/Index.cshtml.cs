using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OrganizationIntranet.Contracts;
using OrganizationIntranet.UI;

namespace OrganizationIntranet.Pages.Publications;
[Authorize]
public class IndexModel(ApiClient api) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? Kind { get; set; }
    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    [BindProperty(SupportsGet = true)] public int PageNumber { get; set; } = 1;
    public PublicationListDto Result { get; set; } = new([], 0, 1, 20);
    public async Task OnGetAsync() => Result = await api.GetAsync<PublicationListDto>($"api/publications?page={PageNumber}&kind={Uri.EscapeDataString(Kind ?? "")}&search={Uri.EscapeDataString(Search ?? "")}");
}
