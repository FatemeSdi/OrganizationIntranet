using OrganizationIntranet.Contracts;
using OrganizationIntranet.Domain.Entities;

namespace OrganizationIntranet.Application.Abstractions;

/// <summary>دسترسی به داده‌ی ورود: تنظیمات و چالش‌های موقت.</summary>
public interface IAuthenticationRepository
{
    /// <summary>ردیف تنظیمات ورود (یک ردیف؛ JSON و Revision).</summary>
    Task<AuthenticationSettings> SettingsAsync();
    /// <summary>چالش با شناسه‌ی هش‌شده‌ی توکن.</summary>
    Task<AuthenticationChallenge?> FindChallengeAsync(string id);
    /// <summary>چالش تازه را اضافه می‌کند.</summary>
    void AddChallenge(AuthenticationChallenge challenge);
    /// <summary>تغییرات را ذخیره می‌کند؛ هم‌زمانی با ستون نسخه کنترل می‌شود.</summary>
    Task SaveAsync();
}

/// <summary>سرویس‌های بیرونی و رمزنگاری ورود: Data Protection، TOTP، LDAP، پیامک و ثبت رویدادهای امنیتی.</summary>
public interface IAuthenticationProviders
{
    /// <summary>مقدار حساس (راز یا کلید) را رمزگذاری می‌کند.</summary>
    string Protect(string value);
    /// <summary>مقدار رمزگذاری‌شده را رمزگشایی می‌کند.</summary>
    string Unprotect(string value);
    /// <summary>راز تصادفی ۲۰ بایتی (Base32) برای Google Authenticator.</summary>
    string NewAuthenticatorSecret();
    /// <summary>کد ۶ رقمی TOTP را با پنجره‌ی ±۱ گام می‌سنجد و گام مطابق را برمی‌گرداند.</summary>
    bool VerifyAuthenticator(string secret, string code, out long step);
    /// <summary>رمز را از راه LDAPS (bind با UPN) بررسی می‌کند.</summary>
    Task<bool> VerifyLdapAsync(AuthenticationSettingsDto settings, string username, string password);
    /// <summary>کد تأیید را از راه درگاه پیامک می‌فرستد.</summary>
    Task SendSmsAsync(AuthenticationSettingsDto settings, string protectedApiKey, string mobile, string code);
    /// <summary>true وقتی درگاه پیامک داخلی پیکربندی شده است؛ در این حالت نشانی HTTPS و کلید Bearer تنظیمات ورود لازم نیست.</summary>
    bool InternalSmsAvailable => false;
    /// <summary>رویداد امنیتی (ورود، شکست، تغییر رمز...) را بدون هیچ راز یا شماره‌ای در لاگ ثبت می‌کند.</summary>
    void Audit(string action, long? userId, bool success);
}
