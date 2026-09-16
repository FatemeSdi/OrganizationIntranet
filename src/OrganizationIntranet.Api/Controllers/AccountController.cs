using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using OrganizationIntranet.Application.Services;
using OrganizationIntranet.Contracts;

namespace OrganizationIntranet.Api.Controllers;

[ApiController, Route("api/account")]
public sealed class AccountController(IntranetService service, IOptionsMonitor<BearerTokenOptions> tokenOptions) : ControllerBase
{
    [HttpPost("login"), AllowAnonymous, EnableRateLimiting("account")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var profile = await service.LoginAsync(request);
        if (profile is null) return Unauthorized(new OperationResult(false, "نام کاربری یا رمز عبور نامعتبر است"));
        var claims = new List<Claim> {
            new(ClaimTypes.NameIdentifier, profile.UserId.ToString()), new(ClaimTypes.Name, profile.Username),
            new(ClaimTypes.GivenName, profile.Name), new(ClaimTypes.Surname, profile.LastName)
        };
        claims.AddRange(profile.RoleCodes.Select(r => new Claim(ClaimTypes.Role, r)));
        claims.AddRange(profile.PermissionCodes.Select(p => new Claim("permission", p)));
        claims.Add(new Claim("role_names", profile.RoleNames));
        return SignIn(new ClaimsPrincipal(new ClaimsIdentity(claims, BearerTokenDefaults.AuthenticationScheme)), BearerTokenDefaults.AuthenticationScheme);
    }

    [HttpPost("forgot-password"), AllowAnonymous, EnableRateLimiting("account")]
    public async Task<OperationResult> Reset(ResetPasswordRequest request)
    {
        await service.ResetPasswordAsync(request); return new(true, "رمز عبور با موفقیت تغییر کرد");
    }
    [HttpPost("refresh"), AllowAnonymous, EnableRateLimiting("account")]
    public IActionResult Refresh(RefreshRequest request)
    {
        var ticket = tokenOptions.Get(BearerTokenDefaults.AuthenticationScheme).RefreshTokenProtector.Unprotect(request.RefreshToken);
        if (ticket?.Properties.ExpiresUtc is not { } expires || expires <= DateTimeOffset.UtcNow)
            return Unauthorized();
        // Renew the same claims snapshot, as the previous sliding cookie did.
        return SignIn(ticket.Principal, BearerTokenDefaults.AuthenticationScheme);
    }
    [HttpGet("me"), Authorize]
    public async Task<IActionResult> Profile()
    {
        var profile = await service.ProfileAsync(CurrentUserId);
        return profile is null ? NotFound() : Ok(profile);
    }
    [HttpPost("change-password"), Authorize, EnableRateLimiting("account")]
    public async Task<OperationResult> Change(ChangePasswordRequest request)
    {
        await service.ChangePasswordAsync(CurrentUserId, request); return new(true, "رمز عبور با موفقیت تغییر کرد");
    }
    private long CurrentUserId => long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
