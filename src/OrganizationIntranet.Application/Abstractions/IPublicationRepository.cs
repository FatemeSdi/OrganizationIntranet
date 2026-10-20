using OrganizationIntranet.Domain.Entities;

namespace OrganizationIntranet.Application.Abstractions;

/// <summary>دامنه‌ی دید محتوا: All = همه (مدیر)، Published = منتشرشده برای کاربر واردشده، Public = منتشرشده و عمومی برای بازدیدکننده‌ی ناشناس.</summary>
public enum PublicationScope { All, Published, Public }

public interface IPublicationRepository
{
    Task<(List<Publication> Items, int Total)> ListAsync(PublicationScope scope, string? kind, string? search, int page, int pageSize);
    Task<Publication?> FindAsync(long id, PublicationScope scope);
    void Add(Publication publication);
    Task SaveAsync();
}
