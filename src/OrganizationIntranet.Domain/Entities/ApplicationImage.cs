namespace OrganizationIntranet.Domain.Entities;

/// <summary>تصویر کارت یک سامانه (کارت‌های پورتال). جدا از Application نگه داشته می‌شود تا فهرست‌ها بایت‌های تصویر را بارگذاری نکنند.</summary>
public sealed class ApplicationImage
{
    public int ApplicationId { get; set; }
    /// <summary>از روی امضای فایل تعیین می‌شود، نه از نوع اعلام‌شده توسط مرورگر: image/jpeg، image/png یا image/webp.</summary>
    public string ContentType { get; set; } = "";
    public byte[] Data { get; set; } = [];
    public DateTime UpdatedAt { get; set; }
}
