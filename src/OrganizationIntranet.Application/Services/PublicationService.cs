using OrganizationIntranet.Application.Abstractions;
using OrganizationIntranet.Contracts;
using OrganizationIntranet.Domain.Entities;

namespace OrganizationIntranet.Application.Services;

public sealed class PublicationService(IPublicationRepository repository)
{
    private static PublicationDto Map(Publication p) => new(p.PublicationId, p.Title, p.Summary, p.Body, p.Kind, p.IsPublished, p.CreatedAt, p.UpdatedAt, p.PublishedAt);
    public async Task<PublicationListDto> ListAsync(bool publishedOnly, string? kind, string? search, int page)
    {
        if (kind is not null && !PublicationKinds.IsValid(kind)) throw new ArgumentException("نوع محتوا معتبر نیست");
        if (page < 1 || page > 100000 || search?.Length > 200) throw new ArgumentException("پارامترهای جست‌وجو معتبر نیستند");
        var result = await repository.ListAsync(publishedOnly, kind, search?.Trim(), page, 20);
        return new(result.Items.Select(p => Map(p) with { Body = "" }).ToList(), result.Total, page, 20);
    }
    public async Task<PublicationDto> GetAsync(long id, bool publishedOnly) => Map(await repository.FindAsync(id, publishedOnly) ?? throw new KeyNotFoundException());
    public async Task<PublicationDto> SaveAsync(long? id, PublicationEditRequest request, long actor)
    {
        if (actor <= 0) throw new ArgumentException("کاربر معتبر نیست");
        foreach (var (value, max) in new[] { (request.Title, 200), (request.Summary, 1000), (request.Body, 50000) })
            if (string.IsNullOrWhiteSpace(value) || value.Length > max) throw new ArgumentException($"عنوان، خلاصه و متن الزامی هستند؛ حداکثر طول فیلد جاری {max} کاراکتر است");
        if (!PublicationKinds.IsValid(request.Kind)) throw new ArgumentException("نوع محتوا معتبر نیست");
        var now = DateTime.UtcNow;
        var p = id.HasValue ? await repository.FindAsync(id.Value, false) ?? throw new KeyNotFoundException() : new Publication { CreatedAt = now, CreatedBy = actor };
        p.Title = request.Title.Trim(); p.Summary = request.Summary.Trim(); p.Body = request.Body.Trim(); p.Kind = request.Kind;
        p.PublishedAt = request.IsPublished ? p.PublishedAt ?? now : null;
        p.IsPublished = request.IsPublished; p.UpdatedAt = now; p.UpdatedBy = actor;
        if (!id.HasValue) repository.Add(p);
        await repository.SaveAsync();
        return Map(p);
    }
}
