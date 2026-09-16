using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OrganizationIntranet.Contracts;
using OrganizationIntranet.UI;
using OrganizationIntranet.Helper;
using DNTCaptcha.Core;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace OrganizationIntranet.Pages.Account;
public class LoginModel(ApiClient api, IDNTCaptchaValidatorService captcha, IConfiguration configuration) : PageModel
{
    [BindProperty] public string Username { get; set; } = "";
    [BindProperty] public string Password { get; set; } = "";
    [BindProperty] public bool RememberMe { get; set; }
    [BindProperty(SupportsGet = true)] public bool LoggedOut { get; set; }
    [BindProperty(SupportsGet = true)] public string? ReturnUrl { get; set; }
    public void OnGet() { }
    public async Task<IActionResult> OnPostAsync()
    {
        if (!captcha.HasRequestValidCaptchaEntry()) return new JsonResult(new { success = false, message = "کد امنیتی اشتباه است" });
        try
        {
            var token = await api.PostAsync<TokenResponse>("api/account/login", new LoginRequest(Username, Password));
            var profile = await api.GetAsync<ProfileDto>("api/account/me", token.AccessToken);
            if (configuration["Ui:Name"] == "Admin" && !profile.RoleCodes.Contains("ADMIN"))
                return new JsonResult(new { success = false, message = "دسترسی به پنل مدیریت مجاز نیست" });
            var claims = new List<Claim> {
                new(ClaimTypes.NameIdentifier, profile.UserId.ToString()), new(ClaimTypes.Name, profile.Username),
                new(ClaimTypes.GivenName, profile.Name), new(ClaimTypes.Surname, profile.LastName), new("api_token", token.AccessToken), new("api_refresh", token.RefreshToken)
            };
            claims.AddRange(profile.RoleCodes.Select(r => new Claim(ClaimTypes.Role, r)));
            claims.AddRange(profile.PermissionCodes.Select(p => new Claim("permission", p)));
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)),
                new AuthenticationProperties { IsPersistent = RememberMe, ExpiresUtc = DateTimeOffset.UtcNow.AddSeconds(token.ExpiresIn), AllowRefresh = true });
            var redirectUrl = profile.RoleCodes.Contains("ADMIN") ? configuration["Sites:Admin"] + "/Admin?welcome=true" : configuration["Sites:Portal"] + "/Portal";
            if (Url.IsLocalUrl(ReturnUrl)) redirectUrl = ReturnUrl!;
            return new JsonResult(new { success = true, redirectUrl });
        }
        catch (ApiException ex) { return new JsonResult(new { success = false, message = ex.Message }); }
    }
}
