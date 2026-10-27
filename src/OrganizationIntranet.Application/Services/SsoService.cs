using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using OrganizationIntranet.Application.Abstractions;
using OrganizationIntranet.Contracts;
using OrganizationIntranet.Domain.Entities;

namespace OrganizationIntranet.Application.Services;

public sealed class SsoService(ISsoRepository repository, ISsoClients clients)
{
    internal static byte[] Hash(string ticket) => SHA256.HashData(Encoding.ASCII.GetBytes(ticket));

    /// <summary>کاربر واردشده روی کارت سامانه کلیک کرده است: بلیت ۶۰ثانیه‌ای فقط برای همان سامانه.</summary>
    public async Task<SsoStartDto> StartAsync(long userId, int applicationId)
    {
        var target = await repository.TargetAsync(userId, applicationId) ?? throw new KeyNotFoundException();
        var url = clients.LandingUrl(target.ApplicationCode) ?? throw new ArgumentException("ورود یکپارچه برای این سامانه فعال نیست");
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
            rows.Where(r => clients.LandingUrl(r.ApplicationCode) is not null || r.ExternalUsername is not null)
                .Select(r => new SsoAccountDto(r.ApplicationId, r.ApplicationCode, r.ApplicationName, clients.LandingUrl(r.ApplicationCode) is not null, r.HasAccess, r.ExternalUsername)).ToList());
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
}
