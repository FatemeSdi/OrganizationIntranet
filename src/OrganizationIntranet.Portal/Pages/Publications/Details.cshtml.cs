using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OrganizationIntranet.Contracts;
using OrganizationIntranet.UI;

namespace OrganizationIntranet.Pages.Publications;
// برای ناشناس، مطلب غیرعمومی یا پیش‌نویس «یافت نشد» (۴۰۴) است؛ وجود آن لو نمی‌رود.
[AllowAnonymous]
public class DetailsModel(ApiClient api) : PageModel
{
    public PublicationDto Item { get; private set; } = null!;
    public bool IsAuthenticated => User.Identity?.IsAuthenticated == true;
    public async Task OnGetAsync(long id) => Item = await api.GetAsync<PublicationDto>(IsAuthenticated ? $"api/publications/{id}" : $"api/public/publications/{id}");
}
