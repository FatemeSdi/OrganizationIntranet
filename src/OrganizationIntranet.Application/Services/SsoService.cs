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
        if (string.IsNullOrWhiteSpace(target.NationalId))
            throw new ArgumentException("کد ملی شما در اینترانت ثبت نشده است؛ برای ورود یکپارچه با مدیر سامانه تماس بگیرید");
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
        var user = await repository.RedeemAsync(Hash(ticket), applicationCode, DateTime.UtcNow);
        if (user is null || string.IsNullOrWhiteSpace(user.NationalId)) throw new ArgumentException(SingleSignOn.InvalidTicket);
        return new(user.NationalId.Trim(), user.Username, user.Name, user.LastName);
    }
}
