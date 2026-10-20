namespace OrganizationIntranet.Application.Catalogs;

public record OrganizationApplicationDefinition(string Code, string Name, string Description, string Icon);
public static class OrganizationApplicationCatalog
{
    public static IReadOnlyList<OrganizationApplicationDefinition> Items { get; } = [
        new("PAYSLIP", "مشاهده فیش حقوق", "مشاهده و دریافت فیش حقوقی پرسنل", "document"),
        new("ATTENDANCE", "حضور و غیاب", "ثبت و پیگیری حضور و غیاب پرسنل", "clock"),
        new("AUTOMATION", "اتوماسیون اداری فرزین", "گردش مکاتبات و نامه‌های اداری سازمان", "mail"),
        new("GATEWAY", "درگاه واحد سازمان", "خدمات الکترونیک واحدهای بندری و دریانوردی", "building"),
        new("MOBIN", "سامانه مبین", "ثبت و پیگیری درخواست کارها", "message"),
        new("CUSTOMERS", "اطلاعات مشتریان", "سامانه اطلاعات مشتریان بندر شهید رجایی", "users"),
        new("DOCINQUIRY", "استعلام سند", "استعلام و پیگیری وضعیت اسناد ثبت‌شده", "search"),
        new("FILEUPLOAD", "سامانه ورود فایل", "بارگذاری و ثبت فایل‌های ورودی سازمان", "upload"),
        new("FILESHARING", "اشتراک فایل", "اشتراک‌گذاری فایل میان واحدهای مختلف", "folder"),
        new("ARCHIVE", "آرشیو الکترونیکی", "بایگانی و جست‌وجوی اسناد الکترونیکی", "archive"),
        new("TASKS", "مدیریت وظایف", "ثبت و گزارش‌گیری از وظایف محوله", "tasks"),
        new("DEMAND", "دیماند", "مدیریت درخواست‌های عملیاتی بندر", "chart")
    ];
}
