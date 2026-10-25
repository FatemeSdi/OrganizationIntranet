namespace OrganizationIntranet.Pages.Library;

public sealed record LibraryItem(string Title, int? Count);
/// <param name="Icon">مسیرهای SVG ثابت (کد برنامه، نه ورودی کاربر)</param>
public sealed record LibraryGroup(string Id, string Title, string Icon, IReadOnlyList<LibraryItem> Items);

/// <summary>
/// قوانین و مقررات، انتشارات و فرم‌های سازمانی؛ مشترک میان بخش صفحه‌ی اصلی و صفحه‌ی /Library.
/// فعلاً ثابت است؛ با اتصال به API فرم‌ها و اسناد (فاز ۳) فقط منبع این فهرست عوض می‌شود.
/// </summary>
public static class LibraryCatalog
{
    public static readonly IReadOnlyList<LibraryGroup> Groups =
    [
        new("rules", "قوانین و مقررات", "<path d='M4 4h16v16H4Z'/><path d='M8 8h8M8 12h8M8 16h5'/>",
        [
            new("کنوانسیون‌ها", 31), new("مصوبات هیأت عامل", 3), new("دستورالعمل‌ها", 78), new("استانداردها", 20),
            new("اداری و استخدامی", 15), new("آموزش", 9), new("آیین‌نامه‌ها", 6),
        ]),
        new("publications", "انتشارات و پشتیبانی", "<path d='M12 3v12'/><path d='m7 10 5 5 5-5'/><path d='M5 19h14'/>",
        [
            new("راهنمای سیستم مدیریت دانش", 11), new("کتاب‌ها", 18), new("آموزش ارسال فکس در اتوماسیون", 1),
            new("آموزش ارسال پست الکترونیک", 1), new("راهنمای کاربری درگاه واحد سازمان", 1),
            new("راهنمای کاربری پایانه‌داران", 1), new("راهنمای کاربری امور بندری", 1),
        ]),
        new("forms", "فرم‌های سازمانی", "<rect x='4' y='3' width='16' height='18' rx='2'/><path d='M8 8h8M8 12h8M8 16h4'/>",
        [
            new("اداری و مالی", 21), new("فناوری اطلاعات (فاوا)", 18), new("اداره برق و تأسیسات", null),
            new("بیانیه خط‌مشی ISMS", 1), new("گواهینامه استاندارد ISMS", 3),
            new("فهرست گروه‌های تخصصی امنیت اطلاعات", 2), new("آگاهی‌رسانی امنیت اطلاعات", 28),
        ]),
    ];

    public static LibraryGroup? Find(string? id) => Groups.FirstOrDefault(g => g.Id == id);

    /// <summary>یکسان‌سازی برای جست‌وجو: ی/ک عربی، نیم‌فاصله و ارقام فارسی.</summary>
    public static string Normalize(string? text) => string.Concat((text ?? "").Trim().ToLowerInvariant()
        .Replace('ي', 'ی').Replace('ك', 'ک').Replace('‌', ' ')
        .Select(c => c is >= '۰' and <= '۹' ? (char)('0' + (c - '۰')) : c));

    public static bool Matches(LibraryItem item, string? search) =>
        string.IsNullOrWhiteSpace(search) || Normalize(item.Title).Contains(Normalize(search), StringComparison.Ordinal);

    /// <summary>عدد با ارقام فارسی؛ تعداد نامعلوم «—».</summary>
    public static string Fa(int? number) => number is null ? "—" : string.Concat(number.Value.ToString().Select(c => (char)('۰' + (c - '0'))));
}
