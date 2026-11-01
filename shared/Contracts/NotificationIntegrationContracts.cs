namespace OrganizationIntranet.Contracts;

/// <summary>اعلانی که یک سامانه به اینترانت می‌فرستد. ExternalId برای همان رویداد و همان گیرنده در سامانه‌ی مبدأ همیشه ثابت بماند تا اعلان تکراری ساخته نشود. TargetUrl باید هم‌مبدأ سامانه باشد.</summary>
public record IncomingApplicationNotification(string ExternalId, string Username, string Title, string Message,
    string TargetUrl, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, bool IsRead = false);
/// <summary>یک دسته (حداکثر ۱۰۰) اعلان و نشانگر ادامه‌ی همگام‌سازی (NextCursor).</summary>
public record ApplicationNotificationBatch(List<IncomingApplicationNotification> Items, string? NextCursor = null);
/// <summary>نتیجه‌ی ورود دسته: چند اعلان وارد شد و چند مورد (تازه‌تر نبود) نادیده ماند.</summary>
public record NotificationImportResult(int Imported, int Ignored);
