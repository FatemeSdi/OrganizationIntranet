using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OrganizationIntranet.Contracts;
using OrganizationIntranet.UI;

namespace OrganizationIntranet.Pages.Admin.Authentication;

[Authorize(Roles = "ADMIN")]
public class IndexModel(ApiClient api) : PageModel
{
    [BindProperty] public AuthenticationSettingsDto Input { get; set; } = new();
    public async Task OnGetAsync() => Input = await api.GetAsync<AuthenticationSettingsDto>("api/admin/authentication");
    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) { ClearKey(); return Page(); }
        try
        {
            var result = await api.PostAsync<OperationResult>("api/admin/authentication", Input);
            TempData["AuthenticationMessage"] = result.Message;
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToPage("/Account/Login");
        }
        catch (ApiException ex) when (ex.Status != System.Net.HttpStatusCode.Unauthorized && ex.Status != System.Net.HttpStatusCode.Forbidden)
        { ModelState.AddModelError("", ex.Message); ClearKey(); return Page(); }
    }
    private void ClearKey() { Input.SmsApiKey = null; ModelState.Remove("Input.SmsApiKey"); }
}
