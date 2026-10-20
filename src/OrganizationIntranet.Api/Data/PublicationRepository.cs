using Microsoft.EntityFrameworkCore;
using OrganizationIntranet.Application.Abstractions;
using OrganizationIntranet.Domain.Entities;

namespace OrganizationIntranet.Api.Data;

public sealed class PublicationRepository(AppDbContext db) : IPublicationRepository
{
    // Public implies published at the database level too (CK_Publication_PublicRequiresPublished); both are filtered for defence in depth.
    private static IQueryable<Publication> Scoped(IQueryable<Publication> query, PublicationScope scope) => scope switch
    {
        PublicationScope.Published => query.Where(p => p.IsPublished),
        PublicationScope.Public => query.Where(p => p.IsPublished && p.IsPublic),
        _ => query,
    };
    public async Task<(List<Publication> Items, int Total)> ListAsync(PublicationScope scope, string? kind, string? search, int page, int pageSize)
    {
        var query = Scoped(db.Publications.AsNoTracking(), scope);
        if (kind is not null) query = query.Where(p => p.Kind == kind);
        if (!string.IsNullOrEmpty(search)) query = query.Where(p => p.Title.Contains(search) || p.Summary.Contains(search));
        var count = await query.CountAsync();
        var items = await query.OrderByDescending(p => p.PublishedAt ?? p.CreatedAt).ThenByDescending(p => p.PublicationId)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(p => new Publication { PublicationId = p.PublicationId, Title = p.Title, Summary = p.Summary, Kind = p.Kind, IsPublished = p.IsPublished, IsPublic = p.IsPublic, CreatedAt = p.CreatedAt, UpdatedAt = p.UpdatedAt, PublishedAt = p.PublishedAt, Revision = p.Revision }).ToListAsync();
        return (items, count);
    }
    public Task<Publication?> FindAsync(long id, PublicationScope scope) => Scoped(db.Publications, scope).FirstOrDefaultAsync(p => p.PublicationId == id);
    public void Add(Publication publication) => db.Publications.Add(publication);
    public async Task SaveAsync()
    {
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateConcurrencyException) { throw new PublicationConflictException(); }
    }
}
