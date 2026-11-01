using Microsoft.EntityFrameworkCore;
using OrganizationIntranet.Application.Abstractions;
using OrganizationIntranet.Domain.Entities;

namespace OrganizationIntranet.Api.Data;

/// <summary>پیاده‌سازی EF Core داده‌ی مطالب و تصویر شاخص؛ محدوده‌ی دید (مدیر، کاربر، عمومی) در Scoped اعمال می‌شود.</summary>
public sealed class PublicationRepository(AppDbContext db) : IPublicationRepository
{
    // Public implies published at the database level too (CK_Publication_PublicRequiresPublished); both are filtered for defence in depth.
    /// <summary>فیلتر دامنه‌ی دید روی مطالب. منتشرشده و عمومی بایگانی‌شده را شامل نمی‌شوند؛ «همه» (All) برای یافتن مطلب با شناسه است.</summary>
    private static IQueryable<Publication> Scoped(IQueryable<Publication> query, PublicationScope scope) => scope switch
    {
        PublicationScope.Published => query.Where(p => p.IsPublished && !p.IsArchived),
        PublicationScope.Public => query.Where(p => p.IsPublished && p.IsPublic && !p.IsArchived),
        PublicationScope.Draft => query.Where(p => !p.IsPublished && !p.IsArchived),
        PublicationScope.Archived => query.Where(p => p.IsArchived),
        PublicationScope.NotArchived => query.Where(p => !p.IsArchived),
        _ => query,
    };
    /// <summary>فهرست از جدیدترین (تاریخ انتشار یا ایجاد) با فیلتر نوع و جست‌وجو در عنوان و خلاصه؛ فقط ستون‌های لازم خوانده می‌شود (بدون متن کامل).</summary>
    public async Task<(List<Publication> Items, int Total)> ListAsync(PublicationScope scope, string? kind, string? search, int page, int pageSize)
    {
        var query = Scoped(db.Publications.AsNoTracking(), scope);
        if (kind is not null) query = query.Where(p => p.Kind == kind);
        if (!string.IsNullOrEmpty(search)) query = query.Where(p => p.Title.Contains(search) || p.Summary.Contains(search));
        var count = await query.CountAsync();
        var items = await query.OrderByDescending(p => p.PublishedAt ?? p.CreatedAt).ThenByDescending(p => p.PublicationId)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(p => new Publication { PublicationId = p.PublicationId, Title = p.Title, Summary = p.Summary, Kind = p.Kind, IsPublished = p.IsPublished, IsPublic = p.IsPublic, CreatedAt = p.CreatedAt, UpdatedAt = p.UpdatedAt, PublishedAt = p.PublishedAt, Revision = p.Revision, ImageUpdatedAt = p.ImageUpdatedAt, IsArchived = p.IsArchived, ArchivedAt = p.ArchivedAt }).ToListAsync();
        return (items, count);
    }
    public Task<Publication?> FindAsync(long id, PublicationScope scope) => Scoped(db.Publications, scope).FirstOrDefaultAsync(p => p.PublicationId == id);
    public void Add(Publication publication) => db.Publications.Add(publication);
    public Task<PublicationImage?> FindImageAsync(long publicationId, PublicationScope scope) =>
        db.PublicationImages.Where(i => i.PublicationId == publicationId && Scoped(db.Publications, scope).Any(p => p.PublicationId == i.PublicationId)).FirstOrDefaultAsync();
    public void AddImage(PublicationImage image) => db.PublicationImages.Add(image);
    public void RemoveImage(PublicationImage image) => db.PublicationImages.Remove(image);
    public async Task SaveAsync()
    {
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateConcurrencyException) { throw new PublicationConflictException(); }
    }
}
