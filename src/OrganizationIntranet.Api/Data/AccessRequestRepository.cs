using Microsoft.EntityFrameworkCore;
using OrganizationIntranet.Application.Abstractions;
using OrganizationIntranet.Application.Services;
using OrganizationIntranet.Contracts;
using OrganizationIntranet.Domain.Entities;
using AppEntity = OrganizationIntranet.Domain.Entities.Application;

namespace OrganizationIntranet.Api.Data;

/// <summary>پیاده‌سازی EF Core داده‌ی درخواست‌های دسترسی. تعارض هم‌زمانی (Revision) به AccessRequestConflictException تبدیل می‌شود.</summary>
public sealed class AccessRequestRepository(AppDbContext db) : IAccessRequestRepository
{
    /// <summary>سامانه‌های در اختیار کاربر (همان تعریف مشترک).</summary>
    private IQueryable<AppEntity> Available(long userId) => ApplicationAccessQueries.Available(db, userId);
    /// <summary>سامانه‌های قابل درخواست: فعال، غیر «محدود به واحد» (آن‌ها فقط برای واحد‌های انتخاب‌شده دیده می‌شوند)، کاربر فعال، بدون دسترسی فعلی (عمومی‌ها هم دسترسی حساب می‌شوند) و بدون درخواست در انتظار.</summary>
    private IQueryable<AppEntity> Requestable(long userId) => db.Applications.Where(a => a.IsActive && !a.IsUnitRestricted
        && db.Users.Any(u => u.UserId == userId && u.IsActive) && !Available(userId).Any(v => v.ApplicationId == a.ApplicationId)
        && !db.ApplicationAccessRequests.Any(r => r.UserId == userId && r.ApplicationId == a.ApplicationId && r.Status == AccessRequestStatuses.Pending));
    public Task<List<AppEntity>> RequestableAsync(long userId) => Requestable(userId).AsNoTracking().OrderBy(a => a.DisplayOrder).ThenBy(a => a.ApplicationId).ToListAsync();
    public Task<bool> CanRequestAsync(long userId, int applicationId) => Requestable(userId).AnyAsync(a => a.ApplicationId == applicationId);
    public Task<bool> HasAccessAsync(long userId, int applicationId) => Available(userId).AnyAsync(a => a.ApplicationId == applicationId);
    /// <summary>درخواست‌ها همراه سامانه، درخواست‌دهنده و بررسی‌کننده.</summary>
    private IQueryable<ApplicationAccessRequest> Requests => db.ApplicationAccessRequests.Include(r => r.Application).Include(r => r.User).Include(r => r.Reviewer);
    /// <summary>فهرست درخواست‌ها از جدیدترین، با فیلتر کاربر و وضعیت و صفحه‌بندی در خود بانک.</summary>
    public async Task<(List<ApplicationAccessRequest> Items, int Total)> ListAsync(long? userId, string? status, int page, int pageSize)
    {
        var query = Requests.AsNoTracking();
        if (userId.HasValue) query = query.Where(r => r.UserId == userId.Value);
        if (status is not null) query = query.Where(r => r.Status == status);
        var total = await query.CountAsync();
        return (await query.OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.ApplicationAccessRequestId)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(), total);
    }
    public Task<ApplicationAccessRequest?> FindAsync(long id, long? userId = null) => Requests.FirstOrDefaultAsync(r => r.ApplicationAccessRequestId == id && (!userId.HasValue || r.UserId == userId.Value));
    public void Add(ApplicationAccessRequest request) => db.ApplicationAccessRequests.Add(request);
    public void Grant(ApplicationAccessRequest request) => db.ApplicationAccessGrants.Add(new() { ApplicationId = request.ApplicationId, UserId = request.UserId, CreatedAt = DateTime.UtcNow });
    public async Task SaveAsync()
    {
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateConcurrencyException) { throw new AccessRequestConflictException(); }
    }
}
