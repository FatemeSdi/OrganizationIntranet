using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OrganizationIntranet.Contracts;

namespace OrganizationIntranet.UI;

[Authorize]
public class SecurityPageModel(ApiClient api) : PageModel
{
    public AuthenticationStatusDto Status { get; set; } = new(false, false, false, false);
    public AuthenticationMethodsDto Methods { get; set; } = new(false, false, false, false, false, false);
    public AuthenticatorSetupDto? Setup { get; set; }
    [BindProperty] public string Password { get; set; } = "";
    [BindProperty] public string LoginMethod { get; set; } = "password";
    [BindProperty] public string Code { get; set; } = "";
    private async Task LoadAsync()
    {
        Status = await api.GetAsync<AuthenticationStatusDto>("api/account/security");
        Methods = await api.GetAsync<AuthenticationMethodsDto>("api/account/methods");
        if (!Methods.PasswordEnabled) LoginMethod = "ldap";
        if (TempData.Peek("AuthenticatorSetup") is string value) Setup = JsonSerializer.Deserialize<AuthenticatorSetupDto>(value);
    }
    public async Task OnGetAsync() => await LoadAsync();
    public async Task<IActionResult> OnPostAsync()
    {
        try
        {
            Setup = await api.PostAsync<AuthenticatorSetupDto>("api/account/authenticator/setup", new AuthenticatorSetupRequest(Password, LoginMethod));
            TempData["AuthenticatorSetup"] = JsonSerializer.Serialize(Setup);
            ModelState.Clear();
        }
        catch (ApiException ex) when (ex.Status != System.Net.HttpStatusCode.Unauthorized) { ModelState.AddModelError("", ex.Message); }
        Password = "";
        await LoadAsync();
        return Page();
    }
    public async Task<IActionResult> OnPostConfirmAsync()
    {
        await LoadAsync();
        if (Setup is null) return RedirectToPage();
        try
        {
            var result = await api.PostAsync<OperationResult>("api/account/authenticator/confirm", new ConfirmAuthenticatorRequest(Setup.ChallengeToken, Code));
            TempData.Remove("AuthenticatorSetup");
            TempData["AuthenticationMessage"] = result.Message;
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToPage("/Account/Login");
        }
        catch (ApiException ex) when (ex.Status != System.Net.HttpStatusCode.Unauthorized) { ModelState.AddModelError("", ex.Message); return Page(); }
    }
}
