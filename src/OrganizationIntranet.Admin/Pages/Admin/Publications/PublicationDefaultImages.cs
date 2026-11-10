namespace OrganizationIntranet.Pages.Admin.Publications;

/// <summary>
/// تصاویر پیش‌فرض کارت خبر (صحنه‌های بندر در رنگ‌های مختلف) در <c>wwwroot/assets/images/publication-defaults</c>. مدیر کنار انتخاب تصویر یکی را برمی‌دارد؛
/// اگر مطلب بعد از ذخیره تصویر نداشته باشد یکی از همین‌ها خودکار انتخاب می‌شود. نام فایل فقط از همین فهرست ثابت می‌آید (هرگز مسیر از ورودی کاربر ساخته نمی‌شود).
/// </summary>
public static class PublicationDefaultImages
{
    public const string Folder = "assets/images/publication-defaults";
    public static readonly IReadOnlyList<string> Names = ["port-1", "port-2", "port-3", "port-4", "port-5", "port-6"];

    public static bool IsValid(string? name) => name is not null && Names.Contains(name);
    public static string Url(string name) => $"/{Folder}/{name}.png";

    /// <summary>مسیر فایل روی دیسک؛ null اگر نام در فهرست ثابت نباشد یا فایل نباشد.</summary>
    public static string? PathOf(string webRoot, string? name)
    {
        if (!IsValid(name)) return null;
        var path = Path.Combine(webRoot, Folder.Replace('/', Path.DirectorySeparatorChar), name + ".png");
        return File.Exists(path) ? path : null;
    }

    /// <summary>انتخاب خودکار پایدار از روی عنوان (برای یک عنوان همیشه همان تصویر؛ مطالب مختلف تصویرهای مختلف می‌گیرند).</summary>
    public static string Pick(string? title)
    {
        var sum = 0;
        foreach (var c in title ?? "") sum = unchecked(sum * 31 + c);
        return Names[(int)((uint)sum % (uint)Names.Count)];
    }
}
