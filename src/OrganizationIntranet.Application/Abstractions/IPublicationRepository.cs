using OrganizationIntranet.Domain.Entities;

namespace OrganizationIntranet.Application.Abstractions;

/// <summary>دامنه‌ی دید محتوا: All = همه (مدیر)، Published = منتشرشده برای کاربر واردشده، Public = منتشرشده و عمومی برای بازدیدکننده‌ی ناشناس، Draft = فقط پیش‌نویس‌ها (فیلتر فهرست مدیر).</summary>
public enum PublicationScope { All, Published, Public, Draft }

public interface IPublicationRepository
{
    Task<(List<Publication> Items, int Total)> ListAsync(PublicationScope scope, string? kind, string? search, int page, int pageSize);
    Task<Publication?> FindAsync(long id, PublicationScope scope);
    void Add(Publication publication);
    /// <summary>تصویر شاخص مطلب، فقط اگر خود مطلب در این دامنه‌ی دید باشد.</summary>
    Task<PublicationImage?> FindImageAsync(long publicationId, PublicationScope scope);
    void AddImage(PublicationImage image);
    void RemoveImage(PublicationImage image);
    Task SaveAsync();
}
