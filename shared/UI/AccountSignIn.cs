using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OrganizationIntranet.Contracts;

namespace OrganizationIntranet.UI;

public record PendingLogin(string ChallengeToken, string[] Methods, bool RememberMe, string? ReturnUrl);

public static class AccountSignIn
{
    public static async Task<string> CompleteAsync(PageModel page, ApiClient api, IConfiguration configuration, TokenResponse token, bool rememberMe, string? returnUrl)
    {
        var profile = await api.GetAsync<ProfileDto>("api/account/me", token.AccessToken);
        if (configuration["Ui:Name"] == "Admin" && !profile.RoleCodes.Contains("ADMIN"))
            throw new ApiException(System.Net.HttpStatusCode.Forbidden, "دسترسی به پنل مدیریت مجاز نیست");
        var claims = new List<Claim> {
            new(ClaimTypes.NameIdentifier, profile.UserId.ToString()), new(ClaimTypes.Name, profile.Username),
            new(ClaimTypes.GivenName, profile.Name), new(ClaimTypes.Surname, profile.LastName),
            new("api_token", token.AccessToken), new("api_refresh", token.RefreshToken)
        };
        claims.AddRange(profile.RoleCodes.Select(r => new Claim(ClaimTypes.Role, r)));
        claims.AddRange(profile.PermissionCodes.Select(p => new Claim("permission", p)));
        await page.HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)),
            new AuthenticationProperties { IsPersistent = rememberMe, ExpiresUtc = DateTimeOffset.UtcNow.AddSeconds(token.ExpiresIn), AllowRefresh = true });
        if (page.Url.IsLocalUrl(returnUrl)) return returnUrl!;
        return profile.RoleCodes.Contains("ADMIN") ? configuration["Sites:Admin"] + "/Admin?welcome=true" : configuration["Sites:Portal"] + "/Portal";
    }
}
