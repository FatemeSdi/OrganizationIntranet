using System.ComponentModel.DataAnnotations;

namespace OrganizationIntranet.Contracts;

public sealed class AuthenticationSettingsDto
{
    public bool PasswordEnabled { get; set; } = true;
    public bool LdapEnabled { get; set; }
    public bool ForgotPasswordEnabled { get; set; }
    public bool TwoFactorEnabled { get; set; }
    public bool AuthenticatorEnabled { get; set; } = true;
    public bool SmsEnabled { get; set; }
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

public record AuthenticationMethodsDto(bool PasswordEnabled, bool LdapEnabled, bool ForgotPasswordEnabled, bool TwoFactorEnabled, bool AuthenticatorEnabled, bool SmsEnabled);
public record AuthenticationChallengeDto(string ChallengeToken, string[] Methods, int ExpiresIn = 300);
public record VerifyLoginRequest(string ChallengeToken, string Method, string Code);
public record SendLoginSmsRequest(string ChallengeToken);
public record ForgotPasswordRequest(string Username);
public record CompletePasswordResetRequest(string ChallengeToken, string Code, string NewPassword, string ConfirmNewPassword);
public record AuthenticatorSetupRequest(string Password, string LoginMethod = "password");
public record AuthenticatorSetupDto(string ChallengeToken, string Secret, string ProvisioningUri);
public record ConfirmAuthenticatorRequest(string ChallengeToken, string Code);
public record AuthenticationStatusDto(bool AuthenticatorConfigured, bool AuthenticatorAvailable, bool SmsAvailable, bool TwoFactorRequired);
