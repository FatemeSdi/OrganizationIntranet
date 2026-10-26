using Microsoft.EntityFrameworkCore;
using OrganizationIntranet.Application.Abstractions;
using OrganizationIntranet.Domain.Entities;

namespace OrganizationIntranet.Api.Data;

public sealed class SsoRepository(AppDbContext db) : ISsoRepository
{
    public async Task<SsoTarget?> TargetAsync(long userId, int applicationId)
    {
        var app = await ApplicationAccessQueries.Available(db, userId).AsNoTracking()
            .Where(a => a.ApplicationId == applicationId).Select(a => new { a.ApplicationId, a.ApplicationCode }).SingleOrDefaultAsync();
        if (app is null) return null;
        return new(app.ApplicationId, app.ApplicationCode, userId);
    }
    public void AddTicket(SsoTicket ticket) => db.SsoTickets.Add(ticket);
    public async Task<User?> RedeemAsync(byte[] ticketHash, string applicationCode, DateTime now)
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
        return await db.Users.AsNoTracking().SingleAsync(u => u.UserId == userId);
    }
    public Task DeleteExpiredAsync(DateTime before) => db.SsoTickets.Where(t => t.ExpiresAt < before).ExecuteDeleteAsync();
    public Task SaveAsync() => db.SaveChangesAsync();
}
