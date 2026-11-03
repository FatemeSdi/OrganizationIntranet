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

/// <summary>تنظیمات SSO یک سامانه برای پنل مدیریت. کلید فقط به‌صورت هش ذخیره می‌شود؛ HasKey می‌گوید ثبت شده یا نه. Enabled یعنی هم کلید هست و هم آدرس درست ساخته می‌شود.</summary>
public record SsoSettingsDto(int ApplicationId, string ApplicationCode, string ApplicationName, string? BaseUrl, string? SsoPath, bool HasKey, DateTime? KeyChangedAt, string? LandingUrl, bool Enabled);
/// <summary>تغییر تنظیمات SSO. SsoPath خالی = پاک شود. KeyAction یکی از SsoKeyActions است؛ NewKey فقط با Set لازم است.</summary>
public record SsoSettingsRequest(string? SsoPath, string? KeyAction = SsoKeyActions.Keep, string? NewKey = null);
/// <summary>نتیجه‌ی ذخیره. GeneratedKey فقط همین یک بار و فقط بعد از Generate پر است؛ جای دیگری قابل خواندن نیست.</summary>
public record SsoSettingsResultDto(SsoSettingsDto Settings, string? GeneratedKey);
public static class SsoKeyActions
{
    public const string Keep = "keep", Generate = "generate", Set = "set", Remove = "remove";
    public const int MinKeyLength = 32, MaxKeyLength = 200;
}
