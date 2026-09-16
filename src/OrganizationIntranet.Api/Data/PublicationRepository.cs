using Microsoft.EntityFrameworkCore;
using OrganizationIntranet.Application.Abstractions;
using OrganizationIntranet.Domain.Entities;

namespace OrganizationIntranet.Api.Data;

public sealed class PublicationRepository(AppDbContext db) : IPublicationRepository
{
    public async Task<(List<Publication> Items, int Total)> ListAsync(bool publishedOnly, string? kind, string? search, int page, int pageSize)
    {
        var query = db.Publications.AsNoTracking().AsQueryable();
        if (publishedOnly) query = query.Where(p => p.IsPublished);
        if (kind is not null) query = query.Where(p => p.Kind == kind);
        if (!string.IsNullOrEmpty(search)) query = query.Where(p => p.Title.Contains(search) || p.Summary.Contains(search));
        var count = await query.CountAsync();
        var items = await query.OrderByDescending(p => p.PublishedAt ?? p.CreatedAt).ThenByDescending(p => p.PublicationId)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(p => new Publication { PublicationId = p.PublicationId, Title = p.Title, Summary = p.Summary, Kind = p.Kind, IsPublished = p.IsPublished, CreatedAt = p.CreatedAt, UpdatedAt = p.UpdatedAt, PublishedAt = p.PublishedAt }).ToListAsync();
        return (items, count);
    }
    public Task<Publication?> FindAsync(long id, bool publishedOnly) => db.Publications.FirstOrDefaultAsync(p => p.PublicationId == id && (!publishedOnly || p.IsPublished));
    public void Add(Publication publication) => db.Publications.Add(publication);
    public Task SaveAsync() => db.SaveChangesAsync();
}
