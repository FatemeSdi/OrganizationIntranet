namespace OrganizationIntranet.Domain.Entities;

/// <summary>تصویر شاخص یک مطلب (کارت اخبار پورتال). جدا از Publication نگه داشته می‌شود تا فهرست‌ها بایت‌های تصویر را بارگذاری نکنند.</summary>
public sealed class PublicationImage
{
    public long PublicationId { get; set; }
    /// <summary>از روی امضای فایل تعیین می‌شود، نه از نوع اعلام‌شده توسط مرورگر: image/jpeg، image/png یا image/webp.</summary>
    public string ContentType { get; set; } = "";
    public byte[] Data { get; set; } = [];
    public DateTime UpdatedAt { get; set; }
}
