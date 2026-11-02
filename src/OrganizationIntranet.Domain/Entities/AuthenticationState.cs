namespace OrganizationIntranet.Domain.Entities;

/// <summary>تنظیمات ورود و امنیت (جدول Sec.AuthenticationSettings): یک ردیف (Id=1) با JSON تنظیمات، کلید پیامک رمزگذاری‌شده و Revision که با هر تغییر همه‌ی نشست‌ها را باطل می‌کند.</summary>
public sealed class AuthenticationSettings
{
    public int Id { get; set; } = 1;
    public string Json { get; set; } = "{}";
    public string? ProtectedSmsApiKey { get; set; }
    public Guid Revision { get; set; } = Guid.NewGuid();
}

/// <summary>چالش موقت ورود دومرحله‌ای، ورود با کد ملی، بازیابی رمز یا راه‌اندازی Authenticator: ۵ دقیقه اعتبار، حداکثر ۵ تلاش و یک‌بارمصرف. شناسه، هش توکن است نه خود توکن.</summary>
public sealed class AuthenticationChallenge
{
    public string Id { get; set; } = "";
    public long UserId { get; set; }
    public string Purpose { get; set; } = "";
    public string LoginMethod { get; set; } = "password";
    public string SecurityStamp { get; set; } = "";
    public Guid SettingsRevision { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? LastSentAt { get; set; }
    public string? CodeHash { get; set; }
    public string? ProtectedSecret { get; set; }
    public int Attempts { get; set; }
    public bool Consumed { get; set; }
    public Guid Version { get; set; } = Guid.NewGuid();
}
