using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OrganizationIntranet.Authorization;
using OrganizationIntranet.Contracts;

namespace OrganizationIntranet.UI;

public record PendingLogin(string ChallengeToken, string[] Methods, bool RememberMe, string? ReturnUrl);

public static class AccountSignIn
{
    /// <summary>زمان آخرین همگام‌سازی نقش‌ها و مجوزها با API (ثانیه‌ی یونیکس).</summary>
    public const string ClaimsSyncedClaim = "claims_synced";
    /// <summary>هر چند دقیقه یک‌بار نقش و مجوزها از API دوباره خوانده می‌شوند؛ بررسی نهایی همیشه در API و زنده است.</summary>
    public static readonly TimeSpan ClaimsLifetime = TimeSpan.FromMinutes(5);

    public static async Task<string> CompleteAsync(PageModel page, ApiClient api, IConfiguration configuration, TokenResponse token, bool rememberMe, string? returnUrl)
    {
        var profile = await api.GetAsync<ProfileDto>("api/account/me", token.AccessToken);
        var canAdminister = profile.PermissionCodes.Contains(AdminPermissions.IntranetAdmin);
        if (configuration["Ui:Name"] == "Admin" && !canAdminister)
            throw new ApiException(System.Net.HttpStatusCode.Forbidden, "دسترسی به پنل مدیریت مجاز نیست");
        var claims = new List<Claim> {
            new(ClaimTypes.NameIdentifier, profile.UserId.ToString()), new(ClaimTypes.Name, profile.Username),
            new("api_token", token.AccessToken), new("api_refresh", token.RefreshToken)
        };
        claims.AddRange(ProfileClaims(profile, DateTimeOffset.UtcNow));
        await page.HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)),
            new AuthenticationProperties { IsPersistent = rememberMe, ExpiresUtc = DateTimeOffset.UtcNow.AddSeconds(token.ExpiresIn), AllowRefresh = true });
        if (page.Url.IsLocalUrl(returnUrl)) return returnUrl!;
        return canAdminister ? configuration["Sites:Admin"] + "/Admin?welcome=true" : configuration["Sites:Portal"] + "/Portal";
    }

    private static IEnumerable<Claim> ProfileClaims(ProfileDto profile, DateTimeOffset now)
    {
        yield return new(ClaimTypes.GivenName, profile.Name);
        yield return new(ClaimTypes.Surname, profile.LastName);
        foreach (var role in profile.RoleCodes) yield return new(ClaimTypes.Role, role);
        foreach (var permission in profile.PermissionCodes) yield return new(PermissionClaimTypes.Permission, permission);
        yield return new(ClaimsSyncedClaim, now.ToUnixTimeSeconds().ToString());
    }

    public static bool ClaimsAreStale(ClaimsPrincipal? principal, DateTimeOffset now) =>
        principal?.Identity?.IsAuthenticated == true &&
        (!long.TryParse(principal.FindFirst(ClaimsSyncedClaim)?.Value, out var synced) || now - DateTimeOffset.FromUnixTimeSeconds(synced) > ClaimsLifetime);

    /// <summary>
    /// نقش و مجوزهای کوکی را از پروفایل فعلی API به‌روز می‌کند (برای نمایش منو و صفحه در UI).
    /// نشست باطل‌شده (۴۰۱) کاربر را خارج می‌کند؛ خطای موقت ارتباط نشست را نگه می‌دارد و بعداً دوباره تلاش می‌شود.
    /// </summary>
    public static async Task RefreshClaimsAsync(CookieValidatePrincipalContext context, DateTimeOffset now)
    {
        var token = context.Principal?.FindFirst("api_token")?.Value;
        if (string.IsNullOrEmpty(token)) return;
        try
        {
            var api = context.HttpContext.RequestServices.GetRequiredService<ApiClient>();
            var profile = await api.GetAsync<ProfileDto>("api/account/me", token);
            var kept = context.Principal!.Claims.Where(c => c.Type is not ClaimTypes.Role and not PermissionClaimTypes.Permission
                and not ClaimsSyncedClaim and not ClaimTypes.GivenName and not ClaimTypes.Surname);
            context.ReplacePrincipal(new ClaimsPrincipal(new ClaimsIdentity(kept.Concat(ProfileClaims(profile, now)), CookieAuthenticationDefaults.AuthenticationScheme)));
            context.ShouldRenew = true;
        }
        catch (ApiException ex) when (ex.Status is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.NotFound)
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }
        catch (ApiException) { /* ارتباط موقتاً برقرار نیست؛ Claimهای فعلی می‌مانند و API همچنان زنده بررسی می‌کند */ }
    }
}
