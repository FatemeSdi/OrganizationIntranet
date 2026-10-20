using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OrganizationIntranet.Contracts;
using OrganizationIntranet.UI;

namespace OrganizationIntranet.Pages.Publications;
// ناشناس فقط مطالب منتشرشده‌ی «عمومی» را می‌بیند؛ کاربر واردشده همه‌ی مطالب منتشرشده را. تفکیک در API اعمال می‌شود.
[AllowAnonymous]
public class IndexModel(ApiClient api) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? Kind { get; set; }
    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    [BindProperty(SupportsGet = true)] public int PageNumber { get; set; } = 1;
    public bool IsAuthenticated => User.Identity?.IsAuthenticated == true;
    public PublicationListDto Result { get; set; } = new([], 0, 1, 20);
    public async Task OnGetAsync() => Result = await api.GetAsync<PublicationListDto>(
        $"{(IsAuthenticated ? "api/publications" : "api/public/publications")}?page={PageNumber}&kind={Uri.EscapeDataString(Kind ?? "")}&search={Uri.EscapeDataString(Search ?? "")}");
}
