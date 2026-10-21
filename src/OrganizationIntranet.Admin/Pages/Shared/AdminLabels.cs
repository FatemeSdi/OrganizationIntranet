namespace OrganizationIntranet.Pages.Shared;

/// <summary>برچسب فارسی مقادیر ثابت که در فرم‌های پنل مدیریت نمایش داده می‌شوند.</summary>
public static class AdminLabels
{
    public static string Icon(string? icon) => icon switch
    {
        "apps" => "سامانه", "document" => "سند", "clock" => "ساعت و تردد", "mail" => "نامه و ایمیل", "building" => "ساختمان",
        "users" => "کاربران", "folder" => "پوشه", "tasks" => "کارها", "message" => "پیام", "search" => "جست‌وجو",
        "upload" => "بارگذاری", "archive" => "بایگانی", "chart" => "گزارش و نمودار", _ => icon ?? "—"
    };

    public static string NotificationChannel(string? channel) => channel switch
    {
        "Sms" => "پیامک", "Both" => "درون‌برنامه‌ای و پیامک", _ => "درون‌برنامه‌ای"
    };
}
