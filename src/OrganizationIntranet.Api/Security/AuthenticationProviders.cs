using System.DirectoryServices.Protocols;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using OrganizationIntranet.Application.Abstractions;
using OrganizationIntranet.Contracts;
using OtpNet;

namespace OrganizationIntranet.Api.Security;

public sealed class AuthenticationProviders(IDataProtectionProvider protection, IHttpClientFactory clients, ILogger<AuthenticationProviders> logger) : IAuthenticationProviders
{
    private readonly IDataProtector protector = protection.CreateProtector("OrganizationIntranet.Authentication.Secrets.v1");
    public string Protect(string value) => protector.Protect(value);
    public string Unprotect(string value) => protector.Unprotect(value);
    public string NewAuthenticatorSecret() => Base32Encoding.ToString(RandomNumberGenerator.GetBytes(20));
    public bool VerifyAuthenticator(string secret, string code, out long step) =>
        new Totp(Base32Encoding.ToBytes(secret)).VerifyTotp(code, out step, new VerificationWindow(previous: 1, future: 1));
    public Task<bool> VerifyLdapAsync(AuthenticationSettingsDto settings, string username, string password)
    {
        // AD UPN bind: fixed configured domain, canonical local username, no search-filter interpolation.
        if (string.IsNullOrWhiteSpace(password) || username.IndexOfAny(['@', '\\', '/', '\0']) >= 0) return Task.FromResult(false);
        try
        {
            using var connection = new LdapConnection(new LdapDirectoryIdentifier(settings.LdapHost, settings.LdapPort));
            connection.AuthType = AuthType.Basic;
            connection.Timeout = TimeSpan.FromSeconds(10);
            connection.SessionOptions.ProtocolVersion = 3;
            connection.SessionOptions.SecureSocketLayer = true;
            connection.SessionOptions.ReferralChasing = ReferralChasingOptions.None;
            // OS trust validates the server certificate; never install an accept-all callback.
            connection.Bind(new NetworkCredential(username + "@" + settings.LdapDomain, password));
            return Task.FromResult(true);
        }
        catch (LdapException) { logger.LogWarning("LDAP authentication failed"); return Task.FromResult(false); }
    }
    public async Task SendSmsAsync(AuthenticationSettingsDto settings, string protectedApiKey, string mobile, string code)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, settings.SmsEndpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Unprotect(protectedApiKey));
        request.Content = JsonContent.Create(new { to = mobile, sender = settings.SmsSender, message = $"کد تأیید اینترانت: {code}\nاعتبار: ۵ دقیقه" });
        using var response = await clients.CreateClient("authentication-sms").SendAsync(request);
        response.EnsureSuccessStatusCode();
    }
    public void Audit(string action, long? userId, bool success) =>
        logger.LogInformation("Authentication audit: {Action}, UserId={UserId}, Success={Success}", action, userId, success);
}
