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

/// <summary>ورود، خروج و امنیت حساب کاربر (API عمومی احراز هویت): روش‌های ورود، ورود با رمز یا کد ملی، ورود دومرحله‌ای، بازیابی رمز و Google Authenticator. همه‌ی منطق در AuthenticationService است و این کنترلر فقط توکن (Bearer) صادر می‌کند.</summary>
[ApiController, Route("api/account"), EnableRateLimiting("account")]
public sealed class AccountController(AuthenticationService authentication, IntranetService service, IOptionsMonitor<BearerTokenOptions> tokenOptions) : ControllerBase
{
    /// <summary>روش‌های ورود فعال و تنظیمات عمومی آن‌ها را برمی‌گرداند (برای ساخت فرم ورود)؛ بدون نیاز به ورود.</summary>
    [HttpGet("methods"), AllowAnonymous]
    public Task<AuthenticationMethodsDto> Methods() => authentication.MethodsAsync();

    /// <summary>ورود با نام کاربری و رمز (یا LDAP). اگر ورود دومرحله‌ای روشن باشد «چالش» برمی‌گرداند، وگرنه مستقیم توکن می‌دهد.</summary>
    [HttpPost("login"), AllowAnonymous]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var result = await authentication.LoginAsync(request);
        return result.Challenge is not null ? Ok(result.Challenge) : await IssueAsync(result.User!, request.Method);
    }
    /// <summary>مرحله‌ی دوم ورود: کد Google Authenticator یا پیامک را با توکن چالش بررسی می‌کند و در صورت درستی توکن می‌دهد.</summary>
    [HttpPost("verify-login"), AllowAnonymous]
    public async Task<IActionResult> Verify(VerifyLoginRequest request)
    {
        var result = await authentication.VerifyLoginAsync(request);
        return await IssueAsync(result.User, result.LoginMethod);
    }
    /// <summary>ارسال (یا ارسال دوباره‌ی) کد پیامکی ورود دومرحله‌ای برای چالش جاری؛ حداقل ۶۰ ثانیه بین دو ارسال.</summary>
    [HttpPost("send-login-sms"), AllowAnonymous]
    public async Task<OperationResult> SendSms(SendLoginSmsRequest request)
    {
        await authentication.SendLoginSmsAsync(request.ChallengeToken);
        return new(true, "کد تأیید ارسال شد");
    }
    /// <summary>ورود بدون رمز با کد ملی و کد یک‌بارمصرف پیامکی: شروع. همیشه یک چالش برمی‌گرداند تا معلوم نشود کد ملی ثبت شده است یا نه.</summary>
    [HttpPost("national-id/start"), AllowAnonymous]
    public Task<AuthenticationChallengeDto> StartNationalIdLogin(NationalIdLoginRequest request) => authentication.StartNationalIdLoginAsync(request);
    /// <summary>ارسال دوباره‌ی کد پیامکی ورود با کد ملی؛ پاسخ همیشه یکسان است تا وجود حساب لو نرود.</summary>
    [HttpPost("national-id/resend"), AllowAnonymous]
    public async Task<OperationResult> ResendNationalIdCode(SendLoginSmsRequest request)
    {
        await authentication.ResendNationalIdCodeAsync(request.ChallengeToken);
        return new(true, "اگر اطلاعات درست باشد، کد جدید پیامک می‌شود");
    }
    /// <summary>مرحله‌ی دوم ورود با کد ملی: کد پیامکی را بررسی می‌کند و توکن می‌دهد.</summary>
    [HttpPost("national-id/verify"), AllowAnonymous]
    public async Task<IActionResult> VerifyNationalIdLogin(VerifyNationalIdLoginRequest request)
    {
        var result = await authentication.VerifyNationalIdLoginAsync(request);
        return await IssueAsync(result.User, result.LoginMethod);
    }
    /// <summary>توکن Bearer می‌سازد: شناسه، نام، نقش‌ها، مجوزها، مهر امنیتی (security_stamp) و نسخه‌ی تنظیمات ورود را در Claimها می‌گذارد تا با تغییر رمز یا تنظیمات باطل شود.</summary>
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
    /// <summary>شروع بازیابی رمز: فقط با کد پیامکی به شماره‌ی ثبت‌شده؛ پاسخ همیشه یک چالش است تا وجود حساب لو نرود.</summary>
    [HttpPost("forgot-password"), AllowAnonymous]
    public Task<AuthenticationChallengeDto> Forgot(ForgotPasswordRequest request) => authentication.ForgotAsync(request);

    /// <summary>پایان بازیابی رمز: کد پیامکی و رمز جدید را می‌گیرد و رمز را عوض می‌کند (همه‌ی نشست‌ها باطل می‌شوند).</summary>
    [HttpPost("reset-password"), AllowAnonymous]
    public async Task<OperationResult> Reset(CompletePasswordResetRequest request)
    {
        await authentication.ResetAsync(request);
        return new(true, "رمز عبور تغییر کرد؛ دوباره وارد شوید");
    }
    /// <summary>تمدید توکن با Refresh Token؛ فقط اگر کاربر هنوز فعال باشد و مهر امنیتی و نسخه‌ی تنظیمات تغییر نکرده باشد.</summary>
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
    /// <summary>پروفایل و مجوزهای کاربر واردشده؛ UI با آن Claimهای خود را هر ۵ دقیقه تازه می‌کند.</summary>
    [HttpGet("me"), Authorize, DisableRateLimiting]
    public async Task<IActionResult> Profile()
    {
        var profile = await service.ProfileAsync(CurrentUserId);
        return profile is null ? NotFound() : Ok(profile);
    }
    /// <summary>تغییر رمز کاربر واردشده با رمز فعلی؛ همه‌ی توکن‌ها و نشست‌ها باطل می‌شوند.</summary>
    [HttpPost("change-password"), Authorize]
    public async Task<OperationResult> Change(ChangePasswordRequest request)
    {
        await authentication.ChangePasswordAsync(CurrentUserId, request);
        return new(true, "رمز عبور تغییر کرد؛ دوباره وارد شوید");
    }
    /// <summary>وضعیت امنیت حساب کاربر: آیا Google Authenticator فعال است و روش‌های دومرحله‌ای در دسترس چیست.</summary>
    [HttpGet("security"), Authorize]
    public Task<AuthenticationStatusDto> Status() => authentication.StatusAsync(CurrentUserId);
    /// <summary>شروع راه‌اندازی Google Authenticator: با تأیید رمز، راز و لینک otpauth می‌سازد (راز تا تأیید کد، فقط موقت و رمزگذاری‌شده است).</summary>
    [HttpPost("authenticator/setup"), Authorize]
    public Task<AuthenticatorSetupDto> Setup(AuthenticatorSetupRequest request) => authentication.SetupAsync(CurrentUserId, request);
    /// <summary>تأیید راه‌اندازی Google Authenticator با یک کد ۶ رقمی؛ پس از تأیید راز در حساب ذخیره می‌شود.</summary>
    [HttpPost("authenticator/confirm"), Authorize]
    public async Task<OperationResult> Confirm(ConfirmAuthenticatorRequest request)
    {
        await authentication.ConfirmSetupAsync(CurrentUserId, request);
        return new(true, "Google Authenticator فعال شد؛ دوباره وارد شوید");
    }
    /// <summary>شناسه‌ی کاربر واردشده از Claim توکن.</summary>
    private long CurrentUserId => long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
