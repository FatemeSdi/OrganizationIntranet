using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace OrganizationIntranet.Pages.Account
{
    /// <summary>خروج از پنل مدیریت: فقط نشست (کوکی) همین UI بسته می‌شود و کاربر به صفحه‌ی ورود می‌رود.</summary>
    public class LogoutModel : PageModel
    {
        public async Task<IActionResult> OnGetAsync() => await SignOutAndRedirectAsync();

        public async Task<IActionResult> OnPostAsync() => await SignOutAndRedirectAsync();

        private async Task<IActionResult> SignOutAndRedirectAsync()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToPage("/Account/Login", new { loggedOut = true });
        }
    }
}
