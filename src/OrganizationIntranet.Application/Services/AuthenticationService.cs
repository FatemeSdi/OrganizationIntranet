using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using OrganizationIntranet.Application.Abstractions;
using OrganizationIntranet.Contracts;
using OrganizationIntranet.Domain.Entities;

namespace OrganizationIntranet.Application.Services;

public sealed class AuthenticationService(IAuthenticationRepository store, IIntranetRepository users, IPasswordService passwords, IAuthenticationProviders providers)
{
    private const string Invalid = "اطلاعات ورود یا کد تأیید معتبر نیست؛ دوباره تلاش کنید";
    private static string RandomToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static bool MobileValid(string? value) => value is not null && Regex.IsMatch(value, @"^\+?[0-9]{10,15}$");
    private static AuthenticationSettingsDto Read(AuthenticationSettings row)
    {
        var result = JsonSerializer.Deserialize<AuthenticationSettingsDto>(row.Json) ?? new();
        result.Revision = row.Revision;
        result.SmsApiKey = null;
        result.HasSmsApiKey = row.ProtectedSmsApiKey is not null;
        return result;
    }
    public async Task<AuthenticationSettingsDto> SettingsAsync() => Read(await store.SettingsAsync());
    public async Task<AuthenticationMethodsDto> MethodsAsync()
    {
        var s = await SettingsAsync();
        return new(s.PasswordEnabled, s.LdapEnabled, s.ForgotPasswordEnabled, s.TwoFactorEnabled, s.AuthenticatorEnabled, s.SmsEnabled);
    }
    public async Task SaveSettingsAsync(AuthenticationSettingsDto s, long actorId)
    {
        var row = await store.SettingsAsync();
        if (row.Revision != s.Revision) throw new ArgumentException("تنظیمات تغییر کرده است؛ صفحه را دوباره بارگذاری کنید");
        if (!s.PasswordEnabled && !s.LdapEnabled) throw new ArgumentException("حداقل یک روش ورود باید فعال باشد");
        if (s.LdapEnabled && (Uri.CheckHostName(s.LdapHost) == UriHostNameType.Unknown || s.LdapPort is < 1 or > 65535 ||
            !Regex.IsMatch(s.LdapDomain, @"^[a-zA-Z0-9][a-zA-Z0-9.-]{0,252}$")))
            throw new ArgumentException("میزبان، پورت و دامنه LDAP معتبر وارد کنید؛ اتصال فقط LDAPS است");
        if (s.SmsEnabled && (!Uri.TryCreate(s.SmsEndpoint, UriKind.Absolute, out var endpoint) || endpoint.Scheme != "https" || !string.IsNullOrEmpty(endpoint.UserInfo) ||
            (string.IsNullOrWhiteSpace(s.SmsApiKey) && row.ProtectedSmsApiKey is null)))
            throw new ArgumentException("نشانی HTTPS و کلید سرویس پیامک الزامی است");
        if (s.ForgotPasswordEnabled && (!s.PasswordEnabled || !s.SmsEnabled))
            throw new ArgumentException("بازیابی رمز به ورود با رمز و سرویس پیامک فعال نیاز دارد");
        if (s.TwoFactorEnabled && !s.AuthenticatorEnabled && !s.SmsEnabled)
            throw new ArgumentException("برای ورود دومرحله‌ای حداقل یک روش تأیید را فعال کنید");
        var actor = await users.FindUserAsync(actorId) ?? throw new KeyNotFoundException();
        if (s.TwoFactorEnabled && !(s.AuthenticatorEnabled && actor.AuthenticatorSecret is not null) && !(s.SmsEnabled && MobileValid(actor.MobileNumber)))
            throw new ArgumentException("ابتدا Google Authenticator حساب مدیر یا شماره همراه او را آماده کنید");
        if (!string.IsNullOrWhiteSpace(s.SmsApiKey)) row.ProtectedSmsApiKey = providers.Protect(s.SmsApiKey);
        s.SmsApiKey = null;
        row.Revision = Guid.NewGuid();
        s.Revision = row.Revision;
        row.Json = JsonSerializer.Serialize(s);
        await store.SaveAsync();
        providers.Audit("settings-changed", actorId, true);
    }
    private async Task<bool> CredentialsAsync(User user, LoginRequest request, AuthenticationSettingsDto settings)
    {
        if (!user.IsActive || user.LockedUntil > DateTime.UtcNow || string.IsNullOrWhiteSpace(request.Password) || request.Password.Length > 1024) return false;
        return request.Method switch
        {
            "password" => settings.PasswordEnabled && !string.IsNullOrEmpty(user.PasswordHash) && passwords.Verify(user, request.Password),
            "ldap" => settings.LdapEnabled && await providers.VerifyLdapAsync(settings, user.Username, request.Password),
            _ => false
        };
    }
    private async Task FailureAsync(User user, string action)
    {
        user.FailedLoginAttempts++;
        if (user.FailedLoginAttempts >= 5) { user.LockedUntil = DateTime.UtcNow.AddMinutes(15); user.FailedLoginAttempts = 0; }
        await store.SaveAsync();
        providers.Audit(action, user.UserId, false);
    }
    public async Task<(User? User, AuthenticationChallengeDto? Challenge)> LoginAsync(LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || request.Username.Length > 100) throw new ArgumentException(Invalid);
        var s = await SettingsAsync();
        var user = await users.FindUserAsync(request.Username.Trim());
        if (user is null) { providers.Audit("login", null, false); throw new ArgumentException(Invalid); }
        if (!await CredentialsAsync(user, request, s)) { await FailureAsync(user, "login"); throw new ArgumentException(Invalid); }
        if (!s.TwoFactorEnabled)
        {
            await CompleteAsync(user);
            return (user, null);
        }
        var methods = AvailableFactors(user, s);
        if (methods.Length == 0) throw new ArgumentException("روش دومرحله‌ای حساب آماده نیست؛ با مدیر سامانه تماس بگیرید");
        var (token, _) = await CreateChallengeAsync(user, s, "login", request.Method);
        return (null, new(token, methods));
    }
    private static string[] AvailableFactors(User user, AuthenticationSettingsDto s) =>
        new[] { s.AuthenticatorEnabled && user.AuthenticatorSecret is not null ? "authenticator" : null, s.SmsEnabled && MobileValid(user.MobileNumber) ? "sms" : null }.OfType<string>().ToArray();
    private async Task<(string Token, AuthenticationChallenge Challenge)> CreateChallengeAsync(User user, AuthenticationSettingsDto s, string purpose, string method = "password")
    {
        var token = RandomToken();
        var challenge = new AuthenticationChallenge { Id = Hash(token), UserId = user.UserId, Purpose = purpose, LoginMethod = method, SecurityStamp = user.SecurityStamp,
            SettingsRevision = s.Revision, ExpiresAt = DateTime.UtcNow.AddMinutes(5) };
        store.AddChallenge(challenge);
        await store.SaveAsync();
        return (token, challenge);
    }
    private async Task<(AuthenticationChallenge Challenge, User User, AuthenticationSettingsDto Settings)> ChallengeAsync(string token, string purpose)
    {
        if (string.IsNullOrEmpty(token) || token.Length != 64) throw new ArgumentException(Invalid);
        var c = await store.FindChallengeAsync(Hash(token));
        var s = await SettingsAsync();
        if (c is null || c.Purpose != purpose || c.Consumed || c.Attempts >= 5 || c.ExpiresAt <= DateTime.UtcNow || c.SettingsRevision != s.Revision) throw new ArgumentException(Invalid);
        var u = await users.FindUserAsync(c.UserId);
        if (u is null || !u.IsActive || u.SecurityStamp != c.SecurityStamp || u.LockedUntil > DateTime.UtcNow) throw new ArgumentException(Invalid);
        return (c, u, s);
    }
    private async Task CompleteAsync(User user)
    {
        user.FailedLoginAttempts = 0; user.LockedUntil = null; user.LastLogin = DateTime.UtcNow;
        await store.SaveAsync();
        providers.Audit("login", user.UserId, true);
    }
    public async Task SendLoginSmsAsync(string token)
    {
        var (c, u, s) = await ChallengeAsync(token, "login");
        if (!s.TwoFactorEnabled || !s.SmsEnabled || !MobileValid(u.MobileNumber)) throw new ArgumentException(Invalid);
        await SendCodeAsync(token, c, u, s);
    }
    private async Task SendCodeAsync(string token, AuthenticationChallenge c, User u, AuthenticationSettingsDto s)
    {
        if (u.LastSecuritySmsAt > DateTime.UtcNow.AddSeconds(-60)) throw new ArgumentException("برای ارسال مجدد کد یک دقیقه صبر کنید");
        var code = RandomNumberGenerator.GetInt32(0, 1000000).ToString("D6");
        c.CodeHash = Hash(token + code); c.LastSentAt = DateTime.UtcNow; u.LastSecuritySmsAt = c.LastSentAt;
        await store.SaveAsync(); // Optimistic concurrency also reserves the per-account SMS cooldown.
        var row = await store.SettingsAsync();
        await providers.SendSmsAsync(s, row.ProtectedSmsApiKey!, u.MobileNumber!, code);
        providers.Audit("sms-sent", u.UserId, true);
    }
    private static bool SmsMatches(AuthenticationChallenge c, string token, string code) => c.CodeHash is not null &&
        CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(c.CodeHash), Encoding.ASCII.GetBytes(Hash(token + code)));
    private bool TotpMatches(User u, string code)
    {
        if (u.AuthenticatorSecret is null || !providers.VerifyAuthenticator(providers.Unprotect(u.AuthenticatorSecret), code, out var step) || step <= u.LastAuthenticatorStep) return false;
        u.LastAuthenticatorStep = step;
        return true;
    }
    public async Task<(User User, string LoginMethod)> VerifyLoginAsync(VerifyLoginRequest request)
    {
        var (c, u, s) = await ChallengeAsync(request.ChallengeToken, "login");
        c.Attempts++;
        var valid = s.TwoFactorEnabled && IsCode(request.Code) && (request.Method switch
        {
            "sms" => s.SmsEnabled && SmsMatches(c, request.ChallengeToken, request.Code),
            "authenticator" => s.AuthenticatorEnabled && TotpMatches(u, request.Code),
            _ => false
        });
        if (!valid) { await FailureAsync(u, "second-factor"); throw new ArgumentException(Invalid); }
        c.Consumed = true; c.CodeHash = null;
        await CompleteAsync(u);
        return (u, c.LoginMethod);
    }
    private static bool IsCode(string? code) => code is not null && Regex.IsMatch(code, "^[0-9]{6}$");
    public async Task<AuthenticationChallengeDto> ForgotAsync(ForgotPasswordRequest request)
    {
        var s = await SettingsAsync();
        if (!s.ForgotPasswordEnabled || !s.PasswordEnabled || !s.SmsEnabled) throw new ArgumentException("بازیابی رمز غیرفعال است");
        if (string.IsNullOrWhiteSpace(request.Username) || request.Username.Length > 100) throw new ArgumentException("نام کاربری را وارد کنید");
        var u = await users.FindUserAsync(request.Username.Trim());
        // Same response for unknown, disabled, unconfigured and throttled accounts.
        var token = RandomToken();
        if (u is { IsActive: true } && MobileValid(u.MobileNumber) && !(u.LastSecuritySmsAt > DateTime.UtcNow.AddSeconds(-60)) && !(u.LockedUntil > DateTime.UtcNow))
        {
            var created = await CreateChallengeAsync(u, s, "reset"); token = created.Token;
            try { await SendCodeAsync(token, created.Challenge, u, s); }
            catch (Exception ex) when (ex is not OperationCanceledException) { providers.Audit("reset-delivery", u.UserId, false); }
        }
        return new(token, ["sms"]);
    }
    public async Task ResetAsync(CompletePasswordResetRequest request)
    {
        var (c, u, s) = await ChallengeAsync(request.ChallengeToken, "reset");
        if (!s.ForgotPasswordEnabled || !s.PasswordEnabled || !s.SmsEnabled) throw new ArgumentException("بازیابی رمز غیرفعال است");
        ValidateNewPassword(request.NewPassword, request.ConfirmNewPassword);
        c.Attempts++;
        if (!IsCode(request.Code) || !SmsMatches(c, request.ChallengeToken, request.Code)) { await FailureAsync(u, "reset"); throw new ArgumentException(Invalid); }
        c.Consumed = true; c.CodeHash = null;
        u.PasswordHash = passwords.Hash(u, request.NewPassword);
        u.SecurityStamp = Guid.NewGuid().ToString("N"); u.FailedLoginAttempts = 0; u.LockedUntil = null;
        await store.SaveAsync();
        providers.Audit("password-reset", u.UserId, true);
    }
    public static void ValidateNewPassword(string password, string confirm)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length is < 10 or > 128 || password != confirm)
            throw new ArgumentException("رمز جدید باید بین ۱۰ تا ۱۲۸ کاراکتر و با تأیید آن یکسان باشد");
    }
    public async Task ChangePasswordAsync(long id, ChangePasswordRequest request)
    {
        var s = await SettingsAsync();
        var u = await users.FindUserAsync(id) ?? throw new KeyNotFoundException();
        if (!await CredentialsAsync(u, new(u.Username, request.CurrentPassword), s))
        { await FailureAsync(u, "password-change"); throw new ArgumentException(Invalid); }
        ValidateNewPassword(request.NewPassword, request.ConfirmNewPassword);
        u.PasswordHash = passwords.Hash(u, request.NewPassword);
        u.SecurityStamp = Guid.NewGuid().ToString("N");
        await store.SaveAsync();
        providers.Audit("password-changed", id, true);
    }
    public async Task<AuthenticationStatusDto> StatusAsync(long id)
    {
        var u = await users.FindUserAsync(id) ?? throw new KeyNotFoundException();
        var s = await SettingsAsync();
        return new(u.AuthenticatorSecret is not null, s.AuthenticatorEnabled, s.SmsEnabled && MobileValid(u.MobileNumber), s.TwoFactorEnabled);
    }
    public async Task<AuthenticatorSetupDto> SetupAsync(long id, AuthenticatorSetupRequest request)
    {
        var s = await SettingsAsync();
        var u = await users.FindUserAsync(id) ?? throw new KeyNotFoundException();
        if (!s.AuthenticatorEnabled || u.AuthenticatorSecret is not null) throw new ArgumentException("راه‌اندازی مجدد مجاز نیست یا این روش غیرفعال است");
        if (!await CredentialsAsync(u, new(u.Username, request.Password, request.LoginMethod), s)) { await FailureAsync(u, "authenticator-setup"); throw new ArgumentException(Invalid); }
        var (token, c) = await CreateChallengeAsync(u, s, "enroll", request.LoginMethod);
        var secret = providers.NewAuthenticatorSecret();
        c.ProtectedSecret = providers.Protect(secret);
        await store.SaveAsync();
        var issuer = "OrganizationIntranet";
        return new(token, secret, $"otpauth://totp/{Uri.EscapeDataString(issuer + ":" + u.Username)}?secret={secret}&issuer={issuer}&algorithm=SHA1&digits=6&period=30");
    }
    public async Task ConfirmSetupAsync(long id, ConfirmAuthenticatorRequest request)
    {
        var (c, u, s) = await ChallengeAsync(request.ChallengeToken, "enroll");
        if (id != u.UserId || !s.AuthenticatorEnabled || u.AuthenticatorSecret is not null) throw new ArgumentException(Invalid);
        c.Attempts++;
        long step = -1;
        if (!IsCode(request.Code) || c.ProtectedSecret is null || !providers.VerifyAuthenticator(providers.Unprotect(c.ProtectedSecret), request.Code, out step))
        { await FailureAsync(u, "authenticator-enroll"); throw new ArgumentException(Invalid); }
        c.Consumed = true; u.AuthenticatorSecret = c.ProtectedSecret; c.ProtectedSecret = null; u.LastAuthenticatorStep = step;
        u.SecurityStamp = Guid.NewGuid().ToString("N");
        await store.SaveAsync();
        providers.Audit("authenticator-enrolled", id, true);
    }
    public async Task<bool> SessionValidAsync(long userId, string? stamp, string? revision)
    {
        var u = await users.FindUserAsync(userId);
        return u is { IsActive: true } && stamp == u.SecurityStamp && revision == (await store.SettingsAsync()).Revision.ToString();
    }
}
