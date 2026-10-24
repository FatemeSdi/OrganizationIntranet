namespace OrganizationIntranet.UI;

/// <summary>متن صفحه‌ی خطای Portal و Admin برای هر کد وضعیت؛ هیچ جزئیات فنی یا متن استثنا به کاربر نشان داده نمی‌شود.</summary>
public sealed record ErrorPageContent(int StatusCode, string Title, string Message, string Hint)
{
    public static ErrorPageContent For(int statusCode) => statusCode switch
    {
        403 => new(403, "عدم دسترسی", "شما مجوز لازم برای دیدن این صفحه یا انجام این کار را ندارید.",
            "اگر فکر می‌کنید باید به این بخش دسترسی داشته باشید، موضوع را با مدیر سامانه یا پشتیبانی فاوا مطرح کنید."),
        404 => new(404, "صفحه پیدا نشد", "صفحه‌ای که دنبال آن هستید وجود ندارد یا جابه‌جا شده است.",
            "نشانی را بررسی کنید یا از صفحه‌ی اصلی ادامه دهید."),
        >= 500 => new(statusCode, "خطای سامانه", "در پردازش درخواست شما مشکلی پیش آمد.",
            "چند لحظه بعد دوباره تلاش کنید. اگر مشکل ادامه داشت، شناسه‌ی پیگیری را به پشتیبانی فاوا بدهید."),
        _ => new(statusCode, "درخواست انجام نشد", "این درخواست قابل انجام نیست.", "از صفحه‌ی اصلی ادامه دهید."),
    };

    /// <summary>
    /// کد صفحه: مسیر AccessDenied همیشه ۴۰۳ است؛ پس از خطای پردازش‌نشده ۵۰۰؛ در re-execute کد اصلی؛ ورودی نامعتبر ۴۰۴.
    /// </summary>
    public static int Resolve(int? routeCode, bool accessDeniedPath, bool unhandledException, int currentStatus)
    {
        if (accessDeniedPath) return 403;
        if (unhandledException) return 500;
        var code = routeCode ?? (currentStatus >= 400 ? currentStatus : 500);
        return code is >= 400 and <= 599 ? code : 404;
    }
}
