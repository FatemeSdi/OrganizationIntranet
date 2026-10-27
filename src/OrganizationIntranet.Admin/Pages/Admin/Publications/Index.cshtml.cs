using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OrganizationIntranet.Contracts;
using OrganizationIntranet.UI;

namespace OrganizationIntranet.Pages.Admin.Publications;
[Authorize(Policy = OrganizationIntranet.Contracts.AdminPermissions.PublicationEditPolicy)]
public class IndexModel(ApiClient api) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? Kind { get; set; }
    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    /// <summary>published، draft یا public؛ مقدار نامعتبر نادیده گرفته می‌شود.</summary>
    [BindProperty(SupportsGet = true)] public string? Status { get; set; }
    [BindProperty(SupportsGet = true)] public int PageNumber { get; set; } = 1;
    public static readonly (string Value, string Label)[] Statuses = [("published", "منتشرشده"), ("draft", "پیش‌نویس"), ("public", "عمومی"), ("archived", "بایگانی‌شده")];
    public PublicationListDto Result { get; set; } = new([], 0, 1, 20);
    public async Task OnGetAsync()
    {
        if (!Statuses.Any(s => s.Value == Status)) Status = null;
        Result = await api.GetAsync<PublicationListDto>($"api/admin/publications?page={PageNumber}&kind={Uri.EscapeDataString(Kind ?? "")}&search={Uri.EscapeDataString(Search ?? "")}&status={Status}");
    }
}
