using OrganizationIntranet.Contracts;

namespace OrganizationIntranet.Pages.Library;

/// <summary>
/// ظاهر کتابخانه‌ی اسناد در پورتال (آیکن هر بخش و قالب اعداد)؛ خود فهرست از API می‌آید (مدیریت در Admin/Library).
/// آیکن‌ها مسیرهای SVG ثابتِ کد برنامه‌اند، نه ورودی کاربر.
/// </summary>
public static class LibraryView
{
    private static readonly Dictionary<string, string> Icons = new()
    {
        [LibraryGroups.Rules] = "<path d='M4 4h16v16H4Z'/><path d='M8 8h8M8 12h8M8 16h5'/>",
        [LibraryGroups.Publications] = "<path d='M12 3v12'/><path d='m7 10 5 5 5-5'/><path d='M5 19h14'/>",
        [LibraryGroups.Forms] = "<rect x='4' y='3' width='16' height='18' rx='2'/><path d='M8 8h8M8 12h8M8 16h4'/>",
    };
    public static string Icon(string groupCode) => Icons.GetValueOrDefault(groupCode, Icons[LibraryGroups.Rules]);

    /// <summary>تب «همه» بدون جست‌وجو فقط پیش‌نمایش دسته‌های هر بخش را نشان می‌دهد.</summary>
    public const int PreviewLimit = 6;

    /// <summary>عدد با ارقام فارسی.</summary>
    public static string Fa(long number) => string.Concat(number.ToString().Select(c => c is >= '0' and <= '9' ? (char)('۰' + (c - '0')) : c));

    /// <summary>حجم فایل خوانا با ارقام فارسی.</summary>
    public static string Size(long bytes) => bytes >= 1048576 ? $"{Fa((long)Math.Round(bytes / 1048576.0 * 10) / 10)} مگابایت".Replace(".", "٫") : $"{Fa(Math.Max(1, bytes / 1024))} کیلوبایت";
}
