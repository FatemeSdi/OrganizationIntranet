using OrganizationIntranet.Domain.Entities;

namespace OrganizationIntranet.Application.Abstractions;

public interface IPublicationRepository
{
    Task<(List<Publication> Items, int Total)> ListAsync(bool publishedOnly, string? kind, string? search, int page, int pageSize);
    Task<Publication?> FindAsync(long id, bool publishedOnly);
    void Add(Publication publication);
    Task SaveAsync();
}
