using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OrganizationIntranet.Contracts;
using OrganizationIntranet.UI;

namespace OrganizationIntranet.Pages.Publications;
[Authorize]
public class DetailsModel(ApiClient api) : PageModel
{
    public PublicationDto Item { get; private set; } = null!;
    public async Task OnGetAsync(long id) => Item = await api.GetAsync<PublicationDto>($"api/publications/{id}");
}
