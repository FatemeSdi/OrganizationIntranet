using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrganizationIntranet.Application.Services;
using OrganizationIntranet.Contracts;

namespace OrganizationIntranet.Api.Controllers;

[ApiController, Route("api/admin/authentication"), Authorize(Roles = "ADMIN")]
public sealed class AuthenticationSettingsController(AuthenticationService authentication) : ControllerBase
{
    [HttpGet]
    public Task<AuthenticationSettingsDto> Get() => authentication.SettingsAsync();
    [HttpPost]
    public async Task<OperationResult> Save(AuthenticationSettingsDto request)
    {
        var previous = await authentication.SettingsAsync();
        if (!request.PasswordEnabled && (User.FindFirstValue("login_method") != "ldap" || !previous.LdapEnabled ||
            previous.LdapHost != request.LdapHost || previous.LdapPort != request.LdapPort || previous.LdapDomain != request.LdapDomain))
            throw new ArgumentException("ابتدا تنظیمات LDAP را ذخیره و با LDAP وارد شوید؛ سپس ورود با رمز محلی را غیرفعال کنید");
        await authentication.SaveSettingsAsync(request, long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!));
        return new(true, "تنظیمات ذخیره شد؛ برای اعمال سیاست جدید دوباره وارد شوید");
    }
}
