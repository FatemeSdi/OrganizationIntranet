using OrganizationIntranet.Application.Abstractions;
using OrganizationIntranet.Contracts;
using OrganizationIntranet.Domain.Entities;

namespace OrganizationIntranet.Application.Services;

public sealed class PublicationService(IPublicationRepository repository)
{
    private static PublicationDto Map(Publication p) => new(p.PublicationId, p.Title, p.Summary, p.Body, p.Kind, p.IsPublished, p.CreatedAt, p.UpdatedAt, p.PublishedAt, p.Revision, p.IsPublic, p.ImageUpdatedAt?.Ticks ?? 0);
    public Task<PublicationListDto> ListAsync(bool publishedOnly, string? kind, string? search, int page) =>
        ListAsync(publishedOnly ? PublicationScope.Published : PublicationScope.All, kind, search, page);
    /// <summary>فهرست محتوای منتشرشده‌ی عمومی برای بازدیدکننده‌ی ناشناس.</summary>
    public Task<PublicationListDto> ListPublicAsync(string? kind, string? search, int page) => ListAsync(PublicationScope.Public, kind, search, page);
    private async Task<PublicationListDto> ListAsync(PublicationScope scope, string? kind, string? search, int page)
    {
        if (kind is not null && !PublicationKinds.IsValid(kind)) throw new ArgumentException("نوع محتوا معتبر نیست");
        if (page < 1 || page > 100000 || search?.Length > 200) throw new ArgumentException("پارامترهای جست‌وجو معتبر نیستند");
        var result = await repository.ListAsync(scope, kind, search?.Trim(), page, 20);
        return new(result.Items.Select(p => Map(p) with { Body = "" }).ToList(), result.Total, page, 20);
    }
    public async Task<PublicationDto> GetAsync(long id, bool publishedOnly) =>
        Map(await repository.FindAsync(id, publishedOnly ? PublicationScope.Published : PublicationScope.All) ?? throw new KeyNotFoundException());
    /// <summary>جزئیات محتوای عمومی؛ پیش‌نویس یا محتوای غیرعمومی برای ناشناس «یافت نشد» است (وجود آن‌ها لو نمی‌رود).</summary>
    public async Task<PublicationDto> GetPublicAsync(long id) => Map(await repository.FindAsync(id, PublicationScope.Public) ?? throw new KeyNotFoundException());
    public async Task<PublicationDto> SaveAsync(long? id, PublicationEditRequest request, long actor)
    {
        if (actor <= 0) throw new ArgumentException("کاربر معتبر نیست");
        foreach (var (value, max) in new[] { (request.Title, 200), (request.Summary, 1000), (request.Body, 50000) })
            if (string.IsNullOrWhiteSpace(value) || value.Length > max) throw new ArgumentException($"عنوان، خلاصه و متن الزامی هستند؛ حداکثر طول فیلد جاری {max} کاراکتر است");
        if (!PublicationKinds.IsValid(request.Kind)) throw new ArgumentException("نوع محتوا معتبر نیست");
        if (request.IsPublic && !request.IsPublished) throw new ArgumentException("نمایش عمومی فقط برای محتوای منتشرشده ممکن است");
        var now = DateTime.UtcNow;
        var p = id.HasValue ? await repository.FindAsync(id.Value, PublicationScope.All) ?? throw new KeyNotFoundException() : new Publication { CreatedAt = now, CreatedBy = actor };
        if (id.HasValue && request.Revision != p.Revision) throw new PublicationConflictException();
        p.Title = request.Title.Trim(); p.Summary = request.Summary.Trim(); p.Body = request.Body.Trim(); p.Kind = request.Kind;
        p.PublishedAt = request.IsPublished ? p.PublishedAt ?? now : null;
        p.IsPublished = request.IsPublished; p.IsPublic = request.IsPublic; p.UpdatedAt = now; p.UpdatedBy = actor;
        p.Revision = checked(p.Revision + 1);
        if (!id.HasValue) repository.Add(p);
        await repository.SaveAsync();
        return Map(p);
    }

    /// <summary>نوع تصویر از امضای فایل (magic bytes)؛ نوع اعلام‌شده‌ی مرورگر پذیرفته نمی‌شود. فقط JPEG، PNG و WebP.</summary>
    public static string? DetectImageType(ReadOnlySpan<byte> data)
    {
        if (data.Length >= 3 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF) return "image/jpeg";
        if (data.Length >= 8 && data[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A })) return "image/png";
        if (data.Length >= 12 && data[..4].SequenceEqual("RIFF"u8) && data[8..12].SequenceEqual("WEBP"u8)) return "image/webp";
        return null;
    }
    public async Task<PublicationDto> SetImageAsync(long id, string? base64, long actor)
    {
        if (actor <= 0) throw new ArgumentException("کاربر معتبر نیست");
        byte[] data;
        try { data = Convert.FromBase64String(base64 ?? ""); }
        catch (FormatException) { throw new ArgumentException("فایل تصویر معتبر نیست"); }
        if (data.Length == 0 || data.Length > PublicationImageRules.MaxBytes) throw new ArgumentException("حجم تصویر باید حداکثر ۲ مگابایت باشد");
        var type = DetectImageType(data) ?? throw new ArgumentException("فقط تصویر JPEG، PNG یا WebP پذیرفته می‌شود");
        var p = await repository.FindAsync(id, PublicationScope.All) ?? throw new KeyNotFoundException();
        var now = DateTime.UtcNow;
        var image = await repository.FindImageAsync(id, PublicationScope.All);
        if (image is null) repository.AddImage(image = new PublicationImage { PublicationId = id });
        image.ContentType = type; image.Data = data; image.UpdatedAt = now;
        // تغییر تصویر Revision را تغییر نمی‌دهد تا فرم ویرایش متن باز در صفحه‌ی دیگر دچار تعارض نشود.
        p.ImageUpdatedAt = now; p.UpdatedAt = now; p.UpdatedBy = actor;
        await repository.SaveAsync();
        return Map(p);
    }
    public async Task RemoveImageAsync(long id, long actor)
    {
        if (actor <= 0) throw new ArgumentException("کاربر معتبر نیست");
        var p = await repository.FindAsync(id, PublicationScope.All) ?? throw new KeyNotFoundException();
        var image = await repository.FindImageAsync(id, PublicationScope.All);
        if (image is null && p.ImageUpdatedAt is null) return;
        if (image is not null) repository.RemoveImage(image);
        p.ImageUpdatedAt = null; p.UpdatedAt = DateTime.UtcNow; p.UpdatedBy = actor;
        await repository.SaveAsync();
    }
    /// <summary>تصویر فقط اگر خود مطلب در دامنه‌ی دید درخواست‌کننده باشد؛ وگرنه «یافت نشد» (وجود پیش‌نویس لو نمی‌رود).</summary>
    public async Task<PublicationImage> GetImageAsync(long id, PublicationScope scope) =>
        await repository.FindImageAsync(id, scope) ?? throw new KeyNotFoundException();
}
