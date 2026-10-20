using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using OrganizationIntranet.Application.Services;
using OrganizationIntranet.Contracts;
using OrganizationIntranet.Domain.Entities;

namespace OrganizationIntranet.Api.Controllers;

[ApiController, Route("api/account"), EnableRateLimiting("account")]
public sealed class AccountController(AuthenticationService authentication, IntranetService service, IOptionsMonitor<BearerTokenOptions> tokenOptions) : ControllerBase
{
    [HttpGet("methods"), AllowAnonymous]
    public Task<AuthenticationMethodsDto> Methods() => authentication.MethodsAsync();

    [HttpPost("login"), AllowAnonymous]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var result = await authentication.LoginAsync(request);
        return result.Challenge is not null ? Ok(result.Challenge) : await IssueAsync(result.User!, request.Method);
    }
    [HttpPost("verify-login"), AllowAnonymous]
    public async Task<IActionResult> Verify(VerifyLoginRequest request)
    {
        var result = await authentication.VerifyLoginAsync(request);
        return await IssueAsync(result.User, result.LoginMethod);
    }
    [HttpPost("send-login-sms"), AllowAnonymous]
    public async Task<OperationResult> SendSms(SendLoginSmsRequest request)
    {
        await authentication.SendLoginSmsAsync(request.ChallengeToken);
        return new(true, "کد تأیید ارسال شد");
    }
    private async Task<IActionResult> IssueAsync(User user, string method)
    {
        var profile = (await service.ProfileAsync(user.UserId))!;
        var settings = await authentication.SettingsAsync();
        var claims = new List<Claim> {
            new(ClaimTypes.NameIdentifier, profile.UserId.ToString()), new(ClaimTypes.Name, profile.Username),
            new(ClaimTypes.GivenName, profile.Name), new(ClaimTypes.Surname, profile.LastName),
            new("security_stamp", user.SecurityStamp), new("auth_revision", settings.Revision.ToString()), new("login_method", method)
        };
        claims.AddRange(profile.RoleCodes.Select(r => new Claim(ClaimTypes.Role, r)));
        claims.AddRange(profile.PermissionCodes.Select(p => new Claim("permission", p)));
        claims.Add(new Claim("role_names", profile.RoleNames));
        return SignIn(new ClaimsPrincipal(new ClaimsIdentity(claims, BearerTokenDefaults.AuthenticationScheme)), BearerTokenDefaults.AuthenticationScheme);
    }
    [HttpPost("forgot-password"), AllowAnonymous]
    public Task<AuthenticationChallengeDto> Forgot(ForgotPasswordRequest request) => authentication.ForgotAsync(request);

    [HttpPost("reset-password"), AllowAnonymous]
    public async Task<OperationResult> Reset(CompletePasswordResetRequest request)
    {
        await authentication.ResetAsync(request);
        return new(true, "رمز عبور تغییر کرد؛ دوباره وارد شوید");
    }
    [HttpPost("refresh"), AllowAnonymous]
    public async Task<IActionResult> Refresh(RefreshRequest request)
    {
        var ticket = tokenOptions.Get(BearerTokenDefaults.AuthenticationScheme).RefreshTokenProtector.Unprotect(request.RefreshToken);
        if (ticket?.Properties.ExpiresUtc is not { } expires || expires <= DateTimeOffset.UtcNow ||
            !long.TryParse(ticket.Principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ||
            !await authentication.SessionValidAsync(id, ticket.Principal.FindFirstValue("security_stamp"), ticket.Principal.FindFirstValue("auth_revision")))
            return Unauthorized();
        return SignIn(ticket.Principal, BearerTokenDefaults.AuthenticationScheme);
    }
    [HttpGet("me"), Authorize, DisableRateLimiting]
    public async Task<IActionResult> Profile()
    {
        var profile = await service.ProfileAsync(CurrentUserId);
        return profile is null ? NotFound() : Ok(profile);
    }
    [HttpPost("change-password"), Authorize]
    public async Task<OperationResult> Change(ChangePasswordRequest request)
    {
        await authentication.ChangePasswordAsync(CurrentUserId, request);
        return new(true, "رمز عبور تغییر کرد؛ دوباره وارد شوید");
    }
    [HttpGet("security"), Authorize]
    public Task<AuthenticationStatusDto> Status() => authentication.StatusAsync(CurrentUserId);
    [HttpPost("authenticator/setup"), Authorize]
    public Task<AuthenticatorSetupDto> Setup(AuthenticatorSetupRequest request) => authentication.SetupAsync(CurrentUserId, request);
    [HttpPost("authenticator/confirm"), Authorize]
    public async Task<OperationResult> Confirm(ConfirmAuthenticatorRequest request)
    {
        await authentication.ConfirmSetupAsync(CurrentUserId, request);
        return new(true, "Google Authenticator فعال شد؛ دوباره وارد شوید");
    }
    private long CurrentUserId => long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
