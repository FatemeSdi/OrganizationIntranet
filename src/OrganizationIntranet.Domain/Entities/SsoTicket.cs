namespace OrganizationIntranet.Domain.Entities;

/// <summary>
/// بلیت یک‌بارمصرف ورود یکپارچه: کاربر واردشده در Portal برای یک سامانه می‌سازد و سرور همان سامانه با کلید خودش تحویل می‌گیرد.
/// خود بلیت ذخیره نمی‌شود، فقط SHA-256 آن.
/// </summary>
public class SsoTicket
{
    public long SsoTicketId { get; set; }
    public byte[] TicketHash { get; set; } = [];
    public long UserId { get; set; }
    public int ApplicationId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? RedeemedAt { get; set; }
    public User User { get; set; } = null!;
    public Application Application { get; set; } = null!;
}
