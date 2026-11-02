using Microsoft.EntityFrameworkCore;
using OrganizationIntranet.Application.Abstractions;
using OrganizationIntranet.Domain.Entities;

namespace OrganizationIntranet.Api.Data;

/// <summary>پیاده‌سازی EF Core داده‌ی ورود یکپارچه: بلیت‌ها و نگاشت نام کاربری هر کاربر در هر سامانه.</summary>
public sealed class SsoRepository(AppDbContext db) : ISsoRepository
{
    /// <summary>سامانه‌ی هدف بلیت؛ null اگر سامانه فعال نیست یا کاربر به آن دسترسی ندارد.</summary>
    public async Task<SsoTarget?> TargetAsync(long userId, int applicationId)
    {
        var app = await ApplicationAccessQueries.Available(db, userId).AsNoTracking()
            .Where(a => a.ApplicationId == applicationId).Select(a => new { a.ApplicationId, a.ApplicationCode, a.BaseUrl }).SingleOrDefaultAsync();
        return app is null ? null : new(app.ApplicationId, app.ApplicationCode, app.BaseUrl, userId);
    }
    public void AddTicket(SsoTicket ticket) => db.SsoTickets.Add(ticket);
    /// <summary>بلیت را اتمیک مصرف می‌کند (UPDATE ... WHERE RedeemedAt IS NULL پس دو مصرف هم‌زمان ممکن نیست)، دسترسی را دوباره می‌سنجد و نام کاربری در سامانه را می‌دهد: نگاشت‌شده یا همان نام اینترانت (مگر نامی که به کاربر دیگری نگاشت شده باشد).</summary>
    public async Task<SsoRedeemed?> RedeemAsync(byte[] ticketHash, string applicationCode, DateTime now)
    {
        var applicationId = await db.Applications.Where(a => a.ApplicationCode == applicationCode).Select(a => (int?)a.ApplicationId).SingleOrDefaultAsync();
        if (applicationId is null) return null;
        // UPDATE ... WHERE RedeemedAt IS NULL: two concurrent redeems of the same ticket cannot both succeed.
        var consumed = await db.SsoTickets.Where(t => t.TicketHash == ticketHash && t.ApplicationId == applicationId && t.RedeemedAt == null && t.ExpiresAt > now)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RedeemedAt, now));
        if (consumed != 1) return null;
        var userId = await db.SsoTickets.Where(t => t.TicketHash == ticketHash).Select(t => t.UserId).SingleAsync();
        // Access is checked again at redeem time: a grant revoked after the click is not honoured.
        if (!await ApplicationAccessQueries.Available(db, userId).AnyAsync(a => a.ApplicationId == applicationId)) return null;
        var user = await db.Users.AsNoTracking().SingleAsync(u => u.UserId == userId);
        var mapped = await db.UserApplicationAccounts.AsNoTracking().Where(a => a.UserId == userId && a.ApplicationId == applicationId).Select(a => a.ExternalUsername).SingleOrDefaultAsync();
        if (mapped is not null) return new(user, mapped);
        // Without a mapping the intranet username is used, unless another user has been mapped to that very account name.
        var lowered = user.Username.ToLower();
        if (await db.UserApplicationAccounts.AnyAsync(a => a.ApplicationId == applicationId && a.ExternalUsername.ToLower() == lowered)) return null;
        return new(user, user.Username);
    }
    public Task<User?> FindUserAsync(long userId) => db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.UserId == userId);
    /// <summary>همه‌ی سامانه‌های فعال با دسترسی فعلی کاربر و نگاشت او (برای صفحه‌ی حساب‌های سامانه‌ای).</summary>
    public async Task<List<SsoAccountRow>> AccountRowsAsync(long userId)
    {
        var granted = await ApplicationAccessQueries.Available(db, userId).AsNoTracking().Select(a => a.ApplicationId).ToListAsync();
        var mappings = await db.UserApplicationAccounts.AsNoTracking().Where(a => a.UserId == userId).ToDictionaryAsync(a => a.ApplicationId, a => a.ExternalUsername);
        var apps = await db.Applications.AsNoTracking().Where(a => a.IsActive).OrderBy(a => a.DisplayOrder).ThenBy(a => a.ApplicationId)
            .Select(a => new { a.ApplicationId, a.ApplicationCode, a.ApplicationName, a.BaseUrl }).ToListAsync();
        return apps.Select(a => new SsoAccountRow(a.ApplicationId, a.ApplicationCode, a.ApplicationName, a.BaseUrl, granted.Contains(a.ApplicationId), mappings.GetValueOrDefault(a.ApplicationId))).ToList();
    }
    public Task<string?> ActiveApplicationCodeAsync(int applicationId) =>
        db.Applications.AsNoTracking().Where(a => a.ApplicationId == applicationId && a.IsActive).Select(a => a.ApplicationCode).SingleOrDefaultAsync();
    public Task<UserApplicationAccount?> FindAccountAsync(long userId, int applicationId) =>
        db.UserApplicationAccounts.SingleOrDefaultAsync(a => a.UserId == userId && a.ApplicationId == applicationId);
    /// <summary>آیا این نام در آن سامانه به کاربر دیگری می‌رسد؟ یعنی نگاشت دیگری است یا نام کاربری اینترانتِ کاربری که خودش نگاشت ندارد (بدون فرق حروف بزرگ و کوچک).</summary>
    public async Task<bool> ApplicationUsernameTakenAsync(int applicationId, string name, long exceptUserId)
    {
        var lowered = name.ToLower();
        if (await db.UserApplicationAccounts.AnyAsync(a => a.ApplicationId == applicationId && a.UserId != exceptUserId && a.ExternalUsername.ToLower() == lowered)) return true;
        return await db.Users.AnyAsync(u => u.UserId != exceptUserId && u.Username.ToLower() == lowered
            && !db.UserApplicationAccounts.Any(a => a.UserId == u.UserId && a.ApplicationId == applicationId));
    }
    public void AddAccount(UserApplicationAccount account) => db.UserApplicationAccounts.Add(account);
    public void RemoveAccount(UserApplicationAccount account) => db.UserApplicationAccounts.Remove(account);
    /// <summary>بلیت‌های قدیمی‌تر از before را پاک می‌کند.</summary>
    public Task DeleteExpiredAsync(DateTime before) => db.SsoTickets.Where(t => t.ExpiresAt < before).ExecuteDeleteAsync();
    public Task SaveAsync() => db.SaveChangesAsync();
}
