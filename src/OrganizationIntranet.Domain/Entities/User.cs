namespace OrganizationIntranet.Domain.Entities;

/// <summary>کاربر اینترانت (جدول Sec.User): هویت، اطلاعات تماس، رمز (هش)، وضعیت امنیتی ورود و نقش‌ها. هرگز «ادمین بودن» با یک پرچم نیست؛ همیشه از راه نقش و مجوز است.</summary>
public class User
{
    public long UserId { get; set; }
    /// <summary>نام کاربری یکتا (بدون فرق حروف بزرگ و کوچک)؛ همان نامی که ورود یکپارچه به سامانه‌ها می‌دهد مگر نگاشت جدا ثبت شده باشد.</summary>
    public string Username { get; set; } = string.Empty;
    /// <summary>کد ملی ده‌رقمی (اختیاری) برای ورود با کد ملی و پیامک. در بانک یکتا نیست؛ اگر چند کاربر یک کد ملی داشته باشند ورود با کد ملی برای آن کد رد می‌شود (fail closed).</summary>
    public string? NationalId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? MobileNumber { get; set; }
    public string? Email { get; set; }
    /// <summary>هش رمز (PasswordHasher)؛ خود رمز هرگز ذخیره نمی‌شود. خالی یعنی ورود با رمز برای این کاربر ممکن نیست.</summary>
    public string? PasswordHash { get; set; }
    /// <summary>مهر امنیتی: با تغییر رمز، نقش یا راه‌اندازی Authenticator عوض می‌شود و همه‌ی توکن‌ها و چالش‌های قبلی را باطل می‌کند.</summary>
    public string SecurityStamp { get; set; } = Guid.NewGuid().ToString("N");
    /// <summary>راز Google Authenticator، رمزگذاری‌شده با Data Protection؛ خالی یعنی راه‌اندازی نشده.</summary>
    public string? AuthenticatorSecret { get; set; }
    /// <summary>آخرین گام زمانی TOTP که پذیرفته شده (-1 = هیچ)؛ جلوی بازپخش همان کد را می‌گیرد.</summary>
    public long LastAuthenticatorStep { get; set; } = -1;
    /// <summary>شمار شکست‌های پیاپی ورود؛ با ۵ شکست حساب ۱۵ دقیقه قفل و شمارنده صفر می‌شود.</summary>
    public int FailedLoginAttempts { get; set; }
    /// <summary>تا این زمان (UTC) ورود و ارسال کد برای حساب بسته است.</summary>
    public DateTime? LockedUntil { get; set; }
    /// <summary>زمان آخرین پیامک امنیتی؛ فاصله‌ی دو پیامک برای هر حساب حداقل ۶۰ ثانیه است.</summary>
    public DateTime? LastSecuritySmsAt { get; set; }
    /// <summary>ستون نسخه برای کنترل هم‌زمانی تغییرات امنیتی حساب (جلوگیری از مصرف دوباره‌ی کد یا گام).</summary>
    public Guid AuthenticationVersion { get; set; } = Guid.NewGuid();
    public bool IsActive { get; set; } = true;
    /// <summary>واحد کاربر در چارت سازمانی (اختیاری)؛ پایه‌ی دیدن سامانه‌ها و اعلان‌ها بر اساس واحد.</summary>
    public int? UnitId { get; set; }
    public OrgUnit? Unit { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLogin { get; set; }

    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    /// <summary>مجوز مستقیم کاربر؛ فعلاً در هیچ‌جای کد استفاده نمی‌شود (مجوز فقط از راه نقش است).</summary>
    public ICollection<UserPermission> UserPermissions { get; set; } = new List<UserPermission>();
    public ICollection<UserNotification> UserNotifications { get; set; } = new List<UserNotification>();
    public ICollection<Notification> CreatedNotifications { get; set; } = new List<Notification>();
}
