using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using OrganizationIntranet.Application.Abstractions;
using OrganizationIntranet.Contracts;
using OrganizationIntranet.Domain.Entities;
using AppEntity = OrganizationIntranet.Domain.Entities.Application;

namespace OrganizationIntranet.Application.Services;

/// <summary>ورود یکپارچه (SSO) با بلیت یک‌بارمصرف (docs/sso.md): صدور بلیت برای کاربر، تحویل آن توسط سرور سامانه و نگاشت نام کاربری کاربر در هر سامانه.</summary>
public sealed class SsoService(ISsoRepository repository)
{
    /// <summary>هش SHA-256 بلیت؛ خود بلیت هرگز ذخیره نمی‌شود.</summary>
    internal static byte[] Hash(string ticket) => SHA256.HashData(Encoding.ASCII.GetBytes(ticket));

    /// <summary>کاربر واردشده روی کارت سامانه کلیک کرده است: بلیت ۶۰ثانیه‌ای فقط برای همان سامانه.</summary>
    public async Task<SsoStartDto> StartAsync(long userId, int applicationId)
    {
        var target = await repository.TargetAsync(userId, applicationId) ?? throw new KeyNotFoundException();
        var url = SsoLanding.Url(target.BaseUrl, target.SsoPath, target.HasSsoKey) ?? throw new ArgumentException("ورود یکپارچه برای این سامانه فعال نیست");
        var ticket = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var now = DateTime.UtcNow;
        repository.AddTicket(new SsoTicket { TicketHash = Hash(ticket), UserId = userId, ApplicationId = target.ApplicationId, CreatedAt = now, ExpiresAt = now.AddSeconds(SingleSignOn.TicketSeconds) });
        await repository.DeleteExpiredAsync(now.AddDays(-1));
        await repository.SaveAsync();
        return new(url + (url.Contains('?') ? "&" : "?") + SingleSignOn.TicketParameter + "=" + ticket);
    }

    /// <summary>سرور سامانه (احرازشده با کلید خودش) بلیت را تحویل می‌گیرد. همه‌ی خطاها یک پیام دارند تا چیزی لو نرود.</summary>
    public async Task<SsoIdentityDto> RedeemAsync(string applicationCode, string? ticket)
    {
        if (ticket is null || !Regex.IsMatch(ticket, SingleSignOn.TicketPattern)) throw new ArgumentException(SingleSignOn.InvalidTicket);
        var redeemed = await repository.RedeemAsync(Hash(ticket), applicationCode, DateTime.UtcNow);
        if (redeemed is null || string.IsNullOrWhiteSpace(redeemed.ApplicationUsername)) throw new ArgumentException(SingleSignOn.InvalidTicket);
        // نام کاربری در همان سامانه (نگاشت‌شده یا همان نام اینترانت)؛ سامانه با همین نام کاربر خودش را پیدا می‌کند و تغییری نمی‌کند.
        return new(redeemed.ApplicationUsername.Trim(), redeemed.User.NationalId?.Trim(), redeemed.User.Name, redeemed.User.LastName);
    }

    /// <summary>سامانه‌های دارای SSO با نام کاربری نگاشت‌شده‌ی این کاربر، برای صفحه‌ی مدیریت.</summary>
    public async Task<SsoAccountsDto> AccountsAsync(long userId)
    {
        var user = await repository.FindUserAsync(userId) ?? throw new KeyNotFoundException();
        var rows = await repository.AccountRowsAsync(userId);
        return new(user.UserId, user.Username, (user.Name + " " + user.LastName).Trim(),
            rows.Where(r => SsoLanding.Url(r.BaseUrl, r.SsoPath, r.HasSsoKey) is not null || r.ExternalUsername is not null)
                .Select(r => new SsoAccountDto(r.ApplicationId, r.ApplicationCode, r.ApplicationName, SsoLanding.Url(r.BaseUrl, r.SsoPath, r.HasSsoKey) is not null, r.HasAccess, r.ExternalUsername)).ToList());
    }

    /// <summary>ثبت (یا با مقدار خالی حذف) نام کاربری کاربر در یک سامانه. حساب هر سامانه فقط به یک کاربر اینترانت تعلق دارد.</summary>
    public async Task<SsoAccountsDto> SetAccountAsync(long userId, int applicationId, string? externalUsername, long actor)
    {
        if (actor <= 0) throw new ArgumentException("کاربر معتبر نیست");
        _ = await repository.FindUserAsync(userId) ?? throw new KeyNotFoundException();
        _ = await repository.ActiveApplicationCodeAsync(applicationId) ?? throw new KeyNotFoundException();
        var name = externalUsername?.Trim();
        var existing = await repository.FindAccountAsync(userId, applicationId);
        if (string.IsNullOrEmpty(name))
        {
            if (existing is not null) { repository.RemoveAccount(existing); await repository.SaveAsync(); }
            return await AccountsAsync(userId);
        }
        if (name.Length > 100 || name.Any(c => char.IsWhiteSpace(c) || char.IsControl(c)))
            throw new ArgumentException("نام کاربری سامانه باید حداکثر ۱۰۰ نویسه و بدون فاصله باشد");
        if (await repository.ApplicationUsernameTakenAsync(applicationId, name, userId))
            throw new ArgumentException("این نام کاربری در این سامانه قبلاً برای کاربر دیگری ثبت شده یا نام کاربری اینترانت کاربر دیگری است");
        var now = DateTime.UtcNow;
        if (existing is null) repository.AddAccount(new UserApplicationAccount { UserId = userId, ApplicationId = applicationId, ExternalUsername = name, CreatedAt = now, UpdatedAt = now, UpdatedBy = actor });
        else { existing.ExternalUsername = name; existing.UpdatedAt = now; existing.UpdatedBy = actor; }
        await repository.SaveAsync();
        return await AccountsAsync(userId);
    }

    /// <summary>هش SHA-256 کلید SSO؛ فقط همین هش ذخیره می‌شود و کلیدِ ارائه‌شده‌ی سامانه با آن سنجیده می‌شود.</summary>
    public static byte[] KeyHash(string key) => SHA256.HashData(Encoding.UTF8.GetBytes(key));

    private static SsoSettingsDto Settings(AppEntity a)
    {
        var url = SsoLanding.Url(a.BaseUrl, a.SsoPath);
        return new(a.ApplicationId, a.ApplicationCode, a.ApplicationName, a.BaseUrl, a.SsoPath, a.SsoKeyHash is not null, a.SsoKeyChangedAt, url, a.SsoKeyHash is not null && url is not null);
    }

    /// <summary>تنظیمات SSO یک سامانه برای صفحه‌ی مدیریت.</summary>
    public async Task<SsoSettingsDto> SettingsAsync(int applicationId) =>
        Settings(await repository.FindApplicationAsync(applicationId) ?? throw new KeyNotFoundException());

    /// <summary>
    /// ذخیره‌ی مسیر و (اختیاری) کلید SSO. کلید یا تصادفی ساخته می‌شود (فقط همین یک بار نشان داده می‌شود) یا کلید موجودِ سامانه ثبت می‌شود؛
    /// در هر دو حالت فقط هش ذخیره می‌شود. کلید باید ۳۲ تا ۲۰۰ نویسه‌ی بدون فاصله باشد، با forbiddenKeys (کلیدهای داخلی UI و کلید اعلان همین سامانه)
    /// یکی نباشد و هش آن مال سامانه‌ی دیگری نباشد، چون کلید یک سامانه نباید سامانه‌ی دیگر را باز کند.
    /// </summary>
    public async Task<SsoSettingsResultDto> SaveSettingsAsync(int applicationId, SsoSettingsRequest request, IReadOnlyCollection<string> forbiddenKeys)
    {
        var app = await repository.FindApplicationAsync(applicationId) ?? throw new KeyNotFoundException();
        var path = string.IsNullOrWhiteSpace(request.SsoPath) ? null : request.SsoPath.Trim();
        if (path is not null && (path.Length > 200 || SsoLanding.Url("https://example.invalid/", path) is null))
            throw new ArgumentException("مسیر SSO باید نسبی باشد، مثل Security/Account/Sso (بدون آدرس کامل، «..»، «\\»، «:» و «#»)");
        string? generated = null;
        switch (request.KeyAction ?? SsoKeyActions.Keep)
        {
            case SsoKeyActions.Keep: break;
            case SsoKeyActions.Remove: app.SsoKeyHash = null; app.SsoKeyChangedAt = DateTime.UtcNow; break;
            case SsoKeyActions.Generate:
                generated = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
                app.SsoKeyHash = KeyHash(generated); app.SsoKeyChangedAt = DateTime.UtcNow; break;
            case SsoKeyActions.Set:
                var key = request.NewKey?.Trim() ?? "";
                if (key.Length is < SsoKeyActions.MinKeyLength or > SsoKeyActions.MaxKeyLength || key.Any(c => char.IsWhiteSpace(c) || char.IsControl(c)))
                    throw new ArgumentException($"کلید SSO باید {SsoKeyActions.MinKeyLength} تا {SsoKeyActions.MaxKeyLength} نویسه و بدون فاصله باشد");
                if (forbiddenKeys.Contains(key)) throw new ArgumentException("این کلید برای کاربرد دیگری (کلید داخلی یا کلید اعلان) استفاده می‌شود؛ کلید جداگانه بگذارید");
                var hash = KeyHash(key);
                if (await repository.SsoKeyHashTakenAsync(hash, applicationId)) throw new ArgumentException("این کلید برای سامانه‌ی دیگری ثبت شده است؛ هر سامانه کلید جدا دارد");
                app.SsoKeyHash = hash; app.SsoKeyChangedAt = DateTime.UtcNow; break;
            default: throw new ArgumentException("عملیات کلید نامعتبر است");
        }
        app.SsoPath = path;
        await repository.SaveAsync();
        return new(Settings(app), generated);
    }
}
