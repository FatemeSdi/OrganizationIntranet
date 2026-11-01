namespace OrganizationIntranet.Domain.Entities;

/// <summary>سامانه‌ی کاتالوگ (جدول App.Application): مشخصات کارت پورتال، پرچم‌های نحوه‌ی دسترسی و روش اطلاع‌رسانی درخواست دسترسی. منطق کسب‌وکار خود سامانه‌ها در اینترانت نیست.</summary>
public class Application
{
    public int ApplicationId { get; set; }
    public string ApplicationName { get; set; } = string.Empty;
    /// <summary>کد یکتای سامانه (حروف بزرگ انگلیسی، رقم، - و _)؛ کلید تنظیمات اتصال هر سامانه (ApplicationIntegrations:{کد}) و نباید پس از ساخت عوض شود.</summary>
    public string ApplicationCode { get; set; } = string.Empty;
    /// <summary>نشانی ورود سامانه: مقصد کارت پورتال، مبدأ مجاز لینک اعلان‌ها و پایه‌ی آدرس ورود یکپارچه.</summary>
    public string? BaseUrl { get; set; }
    public string? Description { get; set; }
    public string? Icon { get; set; }
    /// <summary>ترتیب نمایش در پورتال (عدد کمتر جلوتر). ثبت بدون ترتیب، سامانه را آخرین ردیف می‌کند.</summary>
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
    /// <summary>اطلاع‌رسانی: خود سامانه هم ورود می‌خواهد. ورود را کنترل نمی‌کند.</summary>
    public bool RequiresLogin { get; set; } = true;
    /// <summary>اطلاع‌رسانی: سامانه از اینترنت هم قابل استفاده است. دسترسی شبکه را تغییر نمی‌دهد.</summary>
    public bool IsInternetAccessible { get; set; }
    /// <summary>عمومی: کارت برای همه‌ی کاربران و بازدیدکننده‌ی ناشناس نمایش داده می‌شود و تخصیص دسترسی لازم نیست.</summary>
    public bool IsPublic { get; set; }
    /// <summary>روش اطلاع‌رسانی درخواست دسترسی به ناظران: InApp (درون‌برنامه‌ای)، Sms یا Both.</summary>
    public string AccessRequestNotificationChannels { get; set; } = "InApp";
    public DateTime CreatedAt { get; set; }

    public ICollection<Permission> Permissions { get; set; } = new List<Permission>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
    public ICollection<ApplicationAccess> AccessGrants { get; set; } = new List<ApplicationAccess>();
    public ICollection<ApplicationServiceRegistration> Services { get; set; } = new List<ApplicationServiceRegistration>();
    public ICollection<ApplicationSupervisionGroup> SupervisionGroups { get; set; } = new List<ApplicationSupervisionGroup>();
}
