using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OrganizationIntranet.Contracts;
using OrganizationIntranet.UI;
namespace OrganizationIntranet.Pages.Account;

/// <summary>تنظیمات حساب: تغییر رمز عبور، وضعیت امنیت ورود و خروج از حساب. منطق در API است؛ این صفحه فقط واسط است.</summary>
[Authorize]
public class SettingsModel(ApiClient api) : PageModel
{
    public AuthenticationStatusDto Security { get; set; } = new(false, false, false, false);
    public AuthenticationMethodsDto Methods { get; set; } = new(false, false, false, false, false, false);
    [BindProperty, Required(ErrorMessage = "رمز عبور فعلی را وارد کنید")] public string CurrentPassword { get; set; } = "";
    [BindProperty, Required(ErrorMessage = "رمز عبور جدید را وارد کنید")] public string NewPassword { get; set; } = "";
    [BindProperty, Required(ErrorMessage = "تکرار رمز عبور جدید را وارد کنید")] public string ConfirmNewPassword { get; set; } = "";

    private async Task LoadAsync()
    {
        Security = await api.GetAsync<AuthenticationStatusDto>("api/account/security");
        Methods = await api.GetAsync<AuthenticationMethodsDto>("api/account/methods");
    }
    public async Task OnGetAsync() => await LoadAsync();

    public async Task<IActionResult> OnPostChangePasswordAsync()
    {
        if (ModelState.IsValid)
        {
            try
            {
                var result = await api.PostAsync<OperationResult>("api/account/change-password", new ChangePasswordRequest(CurrentPassword, NewPassword, ConfirmNewPassword));
                // API نشست‌های قبلی را باطل می‌کند؛ کاربر با رمز جدید دوباره وارد می‌شود.
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                TempData["AuthenticationMessage"] = result.Message;
                return RedirectToPage("/Account/Login");
            }
            catch (ApiException ex) when (ex.Status is not (System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden))
            { ModelState.AddModelError("", ex.Message); }
        }
        CurrentPassword = NewPassword = ConfirmNewPassword = "";
        await LoadAsync();
        return Page();
    }
}
