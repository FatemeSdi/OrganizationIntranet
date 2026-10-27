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
/// <summary>هویتی که سامانه با آن کاربر خودش را پیدا می‌کند؛ کلید تطبیق <b>نام کاربری</b> اینترانت است (بدون حساسیت به بزرگی/کوچکی حروف). کد ملی فقط اطلاع‌رسانی است و ممکن است خالی باشد.</summary>
public record SsoIdentityDto(string Username, string? NationalId, string Name, string LastName);

/// <summary>حساب یک کاربر اینترانت در یک سامانه‌ی فعال. ExternalUsername خالی یعنی همان نام کاربری اینترانت استفاده می‌شود.</summary>
public record SsoAccountDto(int ApplicationId, string ApplicationCode, string ApplicationName, bool SingleSignOn, bool HasAccess, string? ExternalUsername);
public record SsoAccountsDto(long UserId, string Username, string FullName, List<SsoAccountDto> Accounts);
/// <summary>نام کاربری کاربر در سامانه؛ خالی = حذف نگاشت و بازگشت به نام کاربری اینترانت.</summary>
public record SsoAccountRequest(string? ExternalUsername);
