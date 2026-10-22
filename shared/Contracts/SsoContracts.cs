namespace OrganizationIntranet.Contracts;

/// <summary>ورود یکپارچه با بلیت یک‌بارمصرف (docs/sso.md).</summary>
public static class SingleSignOn
{
    public const int TicketSeconds = 60;
    public const string TicketParameter = "ticket";
    /// <summary>بلیت: ۳۲ بایت تصادفی با Base64Url بدون padding.</summary>
    public const string TicketPattern = "^[A-Za-z0-9_-]{43}$";
    public const string InvalidTicket = "بلیت ورود نامعتبر، منقضی یا قبلاً استفاده‌شده است";
}
/// <summary>آدرس سامانه همراه بلیت؛ Portal مرورگر را به آن می‌فرستد.</summary>
public record SsoStartDto(string Url);
public record SsoRedeemRequest(string Ticket);
/// <summary>هویتی که سامانه با آن کاربر خودش را پیدا می‌کند؛ کلید تطبیق کد ملی است.</summary>
public record SsoIdentityDto(string NationalId, string Username, string Name, string LastName);
