using OrganizationIntranet.Domain.Entities;

namespace OrganizationIntranet.Application.Abstractions;

/// <summary>دسترسی به داده‌ی ورود اعلان‌های سامانه‌های دیگر؛ پیاده‌سازی در لایه‌ی API.</summary>
public interface INotificationIntegrationRepository
{
    /// <summary>ثبت سرویس اعلان (NOTIFICATIONS) فعال سامانه با کد داده‌شده؛ null یعنی سامانه اعلان نمی‌پذیرد.</summary>
    Task<ApplicationServiceRegistration?> RegistrationAsync(string applicationCode);
    /// <summary>کاربر فعالِ گیرنده با این نام کاربری، فقط اگر به سامانه دسترسی داشته باشد.</summary>
    Task<User?> RecipientAsync(string username, int applicationId);
    /// <summary>اعلان قبلی با همین (سامانه، گیرنده، شناسه‌ی منبع) برای به‌روزرسانی به‌جای تکرار.</summary>
    Task<Notification?> ExistingAsync(int applicationId, long userId, string externalId);
    /// <summary>اعلان تازه را به واحد کار اضافه می‌کند.</summary>
    void Add(Notification notification);
    /// <summary>تغییرات را ذخیره می‌کند.</summary>
    Task SaveAsync();
}
