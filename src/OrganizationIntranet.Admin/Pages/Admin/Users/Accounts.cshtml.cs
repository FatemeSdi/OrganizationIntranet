using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OrganizationIntranet.Contracts;
using OrganizationIntranet.UI;

namespace OrganizationIntranet.Pages.Admin.Users;

/// <summary>
/// نام کاربری یک کاربر در سامانه‌های دارای ورود یکپارچه (SSO)، وقتی با نام کاربری اینترانت فرق دارد.
/// اینترانت همین نام را هنگام ورود به سامانه می‌دهد؛ سامانه‌ها هیچ تغییری نمی‌کنند.
/// </summary>
[Authorize(Policy = OrganizationIntranet.Contracts.AdminPermissions.UserViewPolicy), OrganizationIntranet.Admin.Authorization.PostRequiresPermission(OrganizationIntranet.Contracts.AdminPermissions.UserEdit)]
public class AccountsModel(ApiClient api) : PageModel
{
    [BindProperty(SupportsGet = true)] public long Id { get; set; }
    [BindProperty] public int ApplicationId { get; set; }
    [BindProperty] public string? ExternalUsername { get; set; }
    public SsoAccountsDto? Data { get; private set; }
    public string? ErrorMessage { get; private set; }

    public async Task OnGetAsync() => Data = await api.GetAsync<SsoAccountsDto>($"api/admin/users/{Id}/sso-accounts");

    public async Task<IActionResult> OnPostAsync()
    {
        try
        {
            await api.PostAsync<SsoAccountsDto>($"api/admin/users/{Id}/sso-accounts/{ApplicationId}", new SsoAccountRequest(ExternalUsername));
            TempData["SuccessMessage"] = string.IsNullOrWhiteSpace(ExternalUsername) ? "نگاشت حذف شد؛ از نام کاربری اینترانت استفاده می‌شود." : "نام کاربری در سامانه ذخیره شد.";
            return RedirectToPage(new { id = Id });
        }
        catch (ApiException ex) when ((int)ex.Status is 400 or 409)
        {
            ErrorMessage = ex.Message;
            await OnGetAsync();
            return Page();
        }
    }
}
