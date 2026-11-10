namespace OrganizationIntranet.Contracts;

/// <summary>گزینه‌های ثابت کاتالوگ: آیکن‌های مجاز (باید در application-icons.svg باشند) و نام سرویس اعلان.</summary>
public static class ApplicationCatalogOptions
{
    public static readonly string[] Icons = ["apps", "document", "clock", "mail", "building", "users", "folder", "tasks", "message", "search", "upload", "archive", "chart"];
    public const string NotificationsService = "NOTIFICATIONS";
}

/// <summary>سرویس ثبت‌شده‌ی یک سامانه (کد، نام، نشانی و فعال بودن).</summary>
public record ApplicationServiceRequest(string ServiceCode, string DisplayName, string? EndpointUrl, bool IsActive = true);
/// <summary>تنظیمات کامل یک سامانه برای ذخیره: مشخصات، دسترسی‌ها (کاربر و نقش) و سرویس‌ها.</summary>
public record ApplicationConfigurationRequest(ApplicationRequest Application, List<long> UserIds, List<int> RoleIds, List<ApplicationServiceRequest> Services, List<int>? UnitIds = null);
/// <summary>تنظیمات کامل یک سامانه برای فرم ویرایش، با وضعیت همگام‌سازی هر سرویس.</summary>
public record ApplicationConfigurationDto(ApplicationDto Application, List<long> UserIds, List<int> RoleIds, List<ApplicationServiceRequest> Services, List<ApplicationServiceSyncDto>? SyncStatuses = null, List<int>? UnitIds = null);
/// <summary>آخرین همگام‌سازی و خطای آن برای یک سرویس.</summary>
public record ApplicationServiceSyncDto(string ServiceCode, DateTime? LastSyncedAt, string? LastSyncError);
/// <summary>کارت سامانه در پورتال برای کاربر واردشده: آدرس، آیکن، شمار اعلان خوانده‌نشده و اینکه ورود یکپارچه (SSO) دارد.</summary>
public record PortalApplicationDto(int ApplicationId, string Name, string? Description, string? Url, string Icon,
    bool RequiresLogin, bool IsInternetAccessible, bool IsPublic, int UnreadCount, List<string> Services, bool SingleSignOn = false, long ImageVersion = 0)
{
    /// <summary>آیا سامانه تصویر کارت دارد؟ اگر دارد، کارت پورتال به‌جای آیکن تصویر را نشان می‌دهد.</summary>
    public bool HasImage => ImageVersion != 0;
}
/// <summary>بارگذاری تصویر کارت سامانه؛ نوع فایل از امضای خود فایل تشخیص داده می‌شود.</summary>
public sealed record ApplicationImageUpload(string Base64);
/// <summary>قواعد تصویر کارت سامانه: حداکثر ۲ مگابایت و فقط JPEG، PNG یا WebP؛ تصویر افقی تقریباً ۲ به ۱ (مثلاً ۱۲۰۰×۵۷۶) و بدون متن، متناسب کارت پورتال است.</summary>
public static class ApplicationImageRules
{
    public const int MaxBytes = 2 * 1024 * 1024;
    public const string Accept = "image/jpeg,image/png,image/webp";
}
/// <summary>اعلان سامانه برای زنگ هدر؛ TargetUrl فقط اگر هم‌مبدأ سامانه باشد پر می‌شود.</summary>
public record PortalNotificationDto(long Id, string System, string Title, DateTime CreatedAt, bool Unread, string? TargetUrl = null);
/// <summary>مقصد امن باز کردن یک اعلان.</summary>
public record NotificationDestinationDto(string Url);
