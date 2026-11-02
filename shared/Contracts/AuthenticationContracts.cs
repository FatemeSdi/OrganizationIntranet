using System.ComponentModel.DataAnnotations;

namespace OrganizationIntranet.Contracts;

/// <summary>تنظیمات ورود و امنیت. SmsApiKey فقط نوشتنی است (هرگز برنمی‌گردد و خالی یعنی کلید قبلی بماند)؛ Revision با هر تغییر عوض می‌شود و همه‌ی نشست‌ها را باطل می‌کند.</summary>
public sealed class AuthenticationSettingsDto
{
    public bool PasswordEnabled { get; set; } = true;
    public bool LdapEnabled { get; set; }
    public bool ForgotPasswordEnabled { get; set; }
    public bool TwoFactorEnabled { get; set; }
    public bool AuthenticatorEnabled { get; set; } = true;
    public bool SmsEnabled { get; set; }
    /// <summary>ورود بدون رمز با کد ملی و کد یک‌بارمصرف پیامکی (یک عامل: در اختیار داشتن تلفن). به سرویس پیامک فعال نیاز دارد.</summary>
    public bool NationalIdSmsEnabled { get; set; }
    [MaxLength(253), DisplayFormat(ConvertEmptyStringToNull = false)] public string LdapHost { get; set; } = "";
    [Range(1, 65535)] public int LdapPort { get; set; } = 636;
    [MaxLength(253), DisplayFormat(ConvertEmptyStringToNull = false)] public string LdapDomain { get; set; } = "";
    [MaxLength(2000), DisplayFormat(ConvertEmptyStringToNull = false)] public string SmsEndpoint { get; set; } = "";
    [MaxLength(100), DisplayFormat(ConvertEmptyStringToNull = false)] public string SmsSender { get; set; } = "";
    // Write-only: GET always clears this field; empty means preserve the stored key.
    [MaxLength(2000)] public string? SmsApiKey { get; set; }
    public bool HasSmsApiKey { get; set; }
    public Guid Revision { get; set; }
}

/// <summary>روش‌های ورود فعال، برای ساخت فرم ورود.</summary>
public record AuthenticationMethodsDto(bool PasswordEnabled, bool LdapEnabled, bool ForgotPasswordEnabled, bool TwoFactorEnabled, bool AuthenticatorEnabled, bool SmsEnabled, bool NationalIdSmsEnabled = false);
/// <summary>نام روش‌های ورود که در Claim «login_method» توکن هم ثبت می‌شود.</summary>
public static class LoginMethods
{
    public const string Password = "password";
    public const string Ldap = "ldap";
    public const string NationalIdSms = "national-id-sms";
}
/// <summary>شروع ورود با کد ملی.</summary>
public record NationalIdLoginRequest(string NationalId);
/// <summary>پایان ورود با کد ملی: توکن چالش و کد پیامکی.</summary>
public record VerifyNationalIdLoginRequest(string ChallengeToken, string Code);
/// <summary>چالش ورود: توکن، روش‌های در دسترس و مدت اعتبار (۳۰۰ ثانیه).</summary>
public record AuthenticationChallengeDto(string ChallengeToken, string[] Methods, int ExpiresIn = 300);
/// <summary>مرحله‌ی دوم ورود: توکن چالش، روش (sms یا authenticator) و کد ۶ رقمی.</summary>
public record VerifyLoginRequest(string ChallengeToken, string Method, string Code);
/// <summary>درخواست ارسال (یا ارسال دوباره‌ی) کد پیامکی برای یک چالش.</summary>
public record SendLoginSmsRequest(string ChallengeToken);
/// <summary>شروع بازیابی رمز با نام کاربری.</summary>
public record ForgotPasswordRequest(string Username);
/// <summary>پایان بازیابی رمز: توکن چالش، کد پیامکی و رمز جدید با تأیید آن.</summary>
public record CompletePasswordResetRequest(string ChallengeToken, string Code, string NewPassword, string ConfirmNewPassword);
/// <summary>شروع راه‌اندازی Authenticator با تأیید رمز کاربر.</summary>
public record AuthenticatorSetupRequest(string Password, string LoginMethod = "password");
/// <summary>راز و لینک otpauth برای اسکن در برنامه (فقط یک‌بار نمایش داده می‌شود).</summary>
public record AuthenticatorSetupDto(string ChallengeToken, string Secret, string ProvisioningUri);
/// <summary>تأیید راه‌اندازی با یک کد درست.</summary>
public record ConfirmAuthenticatorRequest(string ChallengeToken, string Code);
/// <summary>وضعیت امنیت حساب کاربر: Authenticator راه‌اندازی شده یا نه و روش‌های در دسترس.</summary>
public record AuthenticationStatusDto(bool AuthenticatorConfigured, bool AuthenticatorAvailable, bool SmsAvailable, bool TwoFactorRequired);
