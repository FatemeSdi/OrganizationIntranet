using System.Text.Json;
using DNTCaptcha.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OrganizationIntranet.Contracts;

namespace OrganizationIntranet.UI;

public class LoginPageModel(ApiClient api, IDNTCaptchaValidatorService captcha, IConfiguration configuration) : PageModel
{
    [BindProperty] public string Username { get; set; } = "";
    [BindProperty] public string Password { get; set; } = "";
    [BindProperty] public string Method { get; set; } = "password";
    [BindProperty] public bool RememberMe { get; set; }
    [BindProperty(SupportsGet = true)] public bool LoggedOut { get; set; }
    [BindProperty(SupportsGet = true)] public string? ReturnUrl { get; set; }
    public AuthenticationMethodsDto Methods { get; set; } = new(false, false, false, false, false, false);
    public string? Error { get; set; }
    public async Task OnGetAsync()
    {
        try { Methods = await api.GetAsync<AuthenticationMethodsDto>("api/account/methods"); }
        catch (ApiException ex) { Error = ex.Message; }
        if (!Methods.PasswordEnabled && Methods.LdapEnabled) Method = "ldap";
    }
    public async Task<IActionResult> OnPostAsync()
    {
        if (!captcha.HasRequestValidCaptchaEntry()) return new JsonResult(new { success = false, message = "کد امنیتی اشتباه است" });
        try
        {
            var token = await api.PostAsync<TokenResponse>("api/account/login", new LoginRequest(Username, Password, Method));
            if (token.ChallengeToken is not null)
            {
                TempData["PendingLogin"] = JsonSerializer.Serialize(new PendingLogin(token.ChallengeToken, token.Methods ?? [], RememberMe, ReturnUrl));
                return new JsonResult(new { success = true, redirectUrl = Url.Page("/Account/TwoFactor") });
            }
            var redirectUrl = await AccountSignIn.CompleteAsync(this, api, configuration, token, RememberMe, ReturnUrl);
            return new JsonResult(new { success = true, redirectUrl });
        }
        catch (ApiException ex) { return new JsonResult(new { success = false, message = ex.Message }); }
    }
}
