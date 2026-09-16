using OrganizationIntranet.Contracts;
using OrganizationIntranet.Domain.Entities;

namespace OrganizationIntranet.Application.Abstractions;

public interface IAuthenticationRepository
{
    Task<AuthenticationSettings> SettingsAsync();
    Task<AuthenticationChallenge?> FindChallengeAsync(string id);
    void AddChallenge(AuthenticationChallenge challenge);
    Task SaveAsync();
}

public interface IAuthenticationProviders
{
    string Protect(string value);
    string Unprotect(string value);
    string NewAuthenticatorSecret();
    bool VerifyAuthenticator(string secret, string code, out long step);
    Task<bool> VerifyLdapAsync(AuthenticationSettingsDto settings, string username, string password);
    Task SendSmsAsync(AuthenticationSettingsDto settings, string protectedApiKey, string mobile, string code);
    void Audit(string action, long? userId, bool success);
}
