using System.Text.RegularExpressions;
using OrganizationIntranet.Application.Abstractions;
using OrganizationIntranet.Contracts;
using OrganizationIntranet.Domain.Entities;
using AppEntity = OrganizationIntranet.Domain.Entities.Application;

namespace OrganizationIntranet.Application.Services;

/// <summary>کاتالوگ سامانه‌ها: ساخت و ویرایش، دسترسی کاربران و نقش‌ها، سرویس‌ها، فهرست پرتال هر کاربر و اعلان‌های او.</summary>
public sealed class ApplicationCatalogService(IApplicationCatalogRepository repository)
{
    private static ApplicationDto Map(AppEntity a) => new(a.ApplicationId, a.ApplicationName, a.ApplicationCode, a.BaseUrl,
        a.Description, a.DisplayOrder, a.IsActive, a.Icon, a.RequiresLogin, a.IsInternetAccessible, a.IsPublic, a.AccessRequestNotificationChannels);
    /// <summary>آدرس باید مطلق، HTTP یا HTTPS و بدون نام کاربری و رمز باشد.</summary>
    private static bool SafeUrl(string? value) => Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && (uri.Scheme == "http" || uri.Scheme == "https") && string.IsNullOrEmpty(uri.UserInfo);
    /// <summary>مقدار اجباری را تمیز می‌کند و اگر خالی یا بلندتر از max باشد با نام فیلد خطا می‌دهد.</summary>
    private static string Required(string? value, int max, string label)
    {
        value = value?.Trim();
        if (string.IsNullOrEmpty(value) || value.Length > max) throw new ArgumentException($"{label} الزامی است و حداکثر {max} کاراکتر دارد");
        return value;
    }
    /// <summary>نشانی ورود الزامی است (کارت پورتال و ورود یکپارچه از آن ساخته می‌شود).</summary>
    private static string RequiredUrl(string? value) => Url(value) ?? throw new ArgumentException("نشانی ورود سامانه الزامی است");
    /// <summary>ترتیب نمایش اختیاری است؛ اگر خالی باشد سامانه آخرین ردیف می‌شود (بعد از بزرگ‌ترین ترتیب بقیه‌ی سامانه‌ها).</summary>
    private async Task<int> OrderAsync(int? requested, int? exceptId) =>
        requested ?? (await repository.ListAsync()).Where(a => a.ApplicationId != exceptId).Select(a => a.DisplayOrder).DefaultIfEmpty(0).Max() + 1;
    /// <summary>آدرس اختیاری را تمیز و اعتبارسنجی می‌کند (حداکثر ۵۰۰ نویسه).</summary>
    private static string? Url(string? value)
    {
        value = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        if (value is not null && (value.Length > 500 || !SafeUrl(value))) throw new ArgumentException("آدرس باید HTTP یا HTTPS و بدون اطلاعات ورود باشد");
        return value;
    }
    /// <summary>فیلدهای مشخصات را روی سامانه اعمال می‌کند: نام، کد (حروف بزرگ انگلیسی، رقم، - و _)، نشانی ورود الزامی، آیکن مجاز، پرچم‌ها و روش اطلاع‌رسانی درخواست دسترسی.</summary>
    private static void Apply(AppEntity entity, ApplicationRequest request, int displayOrder)
    {
        entity.ApplicationName = Required(request.ApplicationName, 150, "نام سامانه");
        entity.ApplicationCode = Required(request.ApplicationCode, 50, "کد سامانه").ToUpperInvariant();
        if (!Regex.IsMatch(entity.ApplicationCode, "^[A-Z0-9_-]+$")) throw new ArgumentException("کد سامانه باید شامل حروف انگلیسی، عدد، خط تیره یا زیرخط باشد");
        entity.BaseUrl = RequiredUrl(request.BaseUrl);
        if (request.Description?.Length > 500) throw new ArgumentException("توضیحات حداکثر ۵۰۰ کاراکتر دارد");
        entity.Description = request.Description?.Trim();
        var icon = string.IsNullOrWhiteSpace(request.Icon) ? "apps" : request.Icon.Trim();
        if (!ApplicationCatalogOptions.Icons.Contains(icon)) throw new ArgumentException("آیکن انتخاب‌شده معتبر نیست");
        entity.Icon = icon;
        entity.DisplayOrder = displayOrder;
        entity.RequiresLogin = request.RequiresLogin;
        entity.IsInternetAccessible = request.IsInternetAccessible;
        entity.IsPublic = request.IsPublic;
        if (!AccessRequestChannels.Valid(request.AccessRequestNotificationChannels)) throw new ArgumentException("روش اعلان درخواست دسترسی معتبر نیست");
        entity.AccessRequestNotificationChannels = request.AccessRequestNotificationChannels;
    }
    /// <summary>همه‌ی سامانه‌ها به ترتیب نمایش.</summary>
    public async Task<List<ApplicationDto>> ListAsync() => (await repository.ListAsync()).Select(Map).ToList();
    /// <summary>تنظیمات کامل یک سامانه: مشخصات، شناسه‌ی کاربران و نقش‌های دارای دسترسی، سرویس‌ها و وضعیت همگام‌سازی.</summary>
    public async Task<ApplicationConfigurationDto> GetAsync(int id)
    {
        var a = await repository.FindAsync(id) ?? throw new KeyNotFoundException();
        return new(Map(a), a.AccessGrants.Where(g => g.UserId.HasValue).Select(g => g.UserId!.Value).ToList(),
            a.AccessGrants.Where(g => g.RoleId.HasValue).Select(g => g.RoleId!.Value).ToList(),
            a.Services.OrderBy(s => s.ServiceCode).Select(s => new ApplicationServiceRequest(s.ServiceCode, s.DisplayName, s.EndpointUrl, s.IsActive)).ToList(),
            a.Services.Select(s => new ApplicationServiceSyncDto(s.ServiceCode, s.LastSyncedAt, s.LastSyncError)).ToList());
    }
    /// <summary>ثبت سامانه‌ی تازه (فعال است؛ ترتیب خالی = آخرین ردیف).</summary>
    public async Task CreateAsync(ApplicationRequest request)
    {
        var a = new AppEntity { CreatedAt = DateTime.UtcNow };
        Apply(a, request, await OrderAsync(request.DisplayOrder, null)); repository.Add(a); await repository.SaveAsync();
    }
    /// <summary>ذخیره‌ی کامل تنظیمات سامانه (مشخصات، دسترسی‌ها و حداکثر ۵۰ سرویس با کد یکتا) در یک تراکنش.</summary>
    public async Task SaveAsync(int id, ApplicationConfigurationRequest request)
    {
        if (request.Application is null || request.UserIds is null || request.RoleIds is null || request.Services is null)
            throw new ArgumentException("تنظیمات سامانه ناقص است");
        var users = request.UserIds.Distinct().ToArray(); var roles = request.RoleIds.Distinct().ToArray();
        if (!await repository.PrincipalsExistAsync(users, roles)) throw new ArgumentException("کاربر یا نقش انتخاب‌شده معتبر نیست");
        if (request.Services.Count > 50) throw new ArgumentException("حداکثر ۵۰ سرویس قابل ثبت است");
        var services = request.Services.Select(s =>
        {
            if (s is null) throw new ArgumentException("سرویس معتبر نیست");
            var code = Required(s.ServiceCode, 50, "کد سرویس").ToUpperInvariant();
            if (!Regex.IsMatch(code, "^[A-Z0-9_-]+$")) throw new ArgumentException("کد سرویس معتبر نیست");
            return new ApplicationServiceRegistration { ServiceCode = code, DisplayName = Required(s.DisplayName, 150, "نام سرویس"),
                EndpointUrl = Url(s.EndpointUrl), IsActive = s.IsActive, CreatedAt = DateTime.UtcNow };
        }).ToArray();
        if (services.Select(s => s.ServiceCode).Distinct().Count() != services.Length) throw new ArgumentException("کد سرویس تکراری است");
        var a = await repository.FindAsync(id) ?? throw new KeyNotFoundException();
        Apply(a, request.Application, await OrderAsync(request.Application.DisplayOrder, id)); repository.SetAccess(a, users, roles); repository.SetServices(a, services);
        await repository.SaveAsync();
    }
    /// <summary>افزودن یا برداشتن یک کاربر/نقش از دسترسی سامانه، بدون ذخیره‌ی بقیه‌ی تنظیمات؛ تکرار یک عملیات بی‌اثر است.</summary>
    public async Task SetAccessAsync(int id, AccessToggleRequest request)
    {
        if (request is null || (request.UserId.HasValue == request.RoleId.HasValue)) throw new ArgumentException("فقط یکی از کاربر یا نقش باید مشخص شود");
        var a = await repository.FindAsync(id) ?? throw new KeyNotFoundException();
        var users = a.AccessGrants.Where(g => g.UserId.HasValue).Select(g => g.UserId!.Value).ToHashSet();
        var roles = a.AccessGrants.Where(g => g.RoleId.HasValue).Select(g => g.RoleId!.Value).ToHashSet();
        if (request.UserId is { } user) { if (request.Granted) users.Add(user); else users.Remove(user); }
        if (request.RoleId is { } role) { if (request.Granted) roles.Add(role); else roles.Remove(role); }
        if (request.Granted && !await repository.PrincipalsExistAsync(request.UserId.HasValue ? [request.UserId.Value] : [], request.RoleId.HasValue ? [request.RoleId.Value] : []))
            throw new ArgumentException("کاربر یا نقش انتخاب‌شده معتبر نیست");
        repository.SetAccess(a, users.ToArray(), roles.ToArray());
        await repository.SaveAsync();
    }
    /// <summary>فعال/غیرفعال کردن سامانه.</summary>
    public async Task ToggleAsync(int id)
    {
        var a = await repository.FindAsync(id) ?? throw new KeyNotFoundException();
        a.IsActive = !a.IsActive; await repository.SaveAsync();
    }
    /// <summary>سامانه‌های فعال و عمومی برای بازدیدکننده‌ی ناشناس؛ فقط داده‌ی نمایشی و آدرس ایمن.</summary>
    public async Task<List<PublicApplicationDto>> PublicAsync() => (await repository.PublicAsync())
        .Select(a => new PublicApplicationDto(a.ApplicationId, a.ApplicationName, a.Description, SafeUrl(a.BaseUrl) ? a.BaseUrl : null,
            ApplicationCatalogOptions.Icons.Contains(a.Icon) ? a.Icon! : "apps", a.RequiresLogin, a.IsInternetAccessible)).ToList();
    /// <summary>سامانه‌های در اختیار کاربر برای پورتال با شمار اعلان خوانده‌نشده‌ی هر سامانه و اینکه ورود یکپارچه دارد یا نه.</summary>
    public async Task<List<PortalApplicationDto>> AvailableAsync(long userId)
    {
        var apps = await repository.AvailableAsync(userId);
        var counts = await repository.UnreadCountsAsync(userId);
        return apps.Select(a => new PortalApplicationDto(a.ApplicationId, a.ApplicationName, a.Description,
            SafeUrl(a.BaseUrl) ? a.BaseUrl : null, ApplicationCatalogOptions.Icons.Contains(a.Icon) ? a.Icon! : "apps",
            a.RequiresLogin, a.IsInternetAccessible, a.IsPublic, counts.GetValueOrDefault(a.ApplicationId),
            a.Services.Where(s => s.IsActive).OrderBy(s => s.ServiceCode).Select(s => s.ServiceCode).ToList(), SsoLanding.Url(a.BaseUrl, a.SsoPath, a.SsoKeyHash is not null) is not null)).ToList();
    }
    /// <summary>آخرین ۵۰ اعلان سامانه‌ها برای زنگ هدر؛ مقصد هر اعلان فقط اگر هم‌مبدأ سامانه باشد برمی‌گردد.</summary>
    public async Task<List<PortalNotificationDto>> NotificationsAsync(long userId) => (await repository.NotificationsAsync(userId, false, 50))
        .Select(n => new PortalNotificationDto(n.UserNotificationId, n.Notification.Application.ApplicationName, n.Notification.Title, n.CreatedAt, !n.IsRead,
            ApplicationLinkPolicy.Destination(n.Notification.Application.BaseUrl, n.Notification.TargetUrl))).ToList();
    /// <summary>اعلان را خوانده‌شده می‌کند و مقصد امن آن را برمی‌گرداند؛ مقصد نامعتبر خطا می‌دهد.</summary>
    public async Task<NotificationDestinationDto> OpenNotificationAsync(long userId, long id)
    {
        var notification = await repository.FindNotificationAsync(userId, id) ?? throw new KeyNotFoundException();
        var destination = ApplicationLinkPolicy.Destination(notification.Notification.Application.BaseUrl, notification.Notification.TargetUrl)
            ?? throw new ArgumentException("آدرس مقصد اعلان معتبر نیست؛ با مدیر سامانه تماس بگیرید");
        if (!notification.IsRead) { notification.IsRead = true; notification.ReadAt = DateTime.UtcNow; await repository.SaveAsync(); }
        return new(destination);
    }
    /// <summary>همه‌ی اعلان‌های خوانده‌نشده‌ی کاربر را خوانده‌شده می‌کند.</summary>
    public async Task MarkNotificationsReadAsync(long userId)
    {
        foreach (var n in await repository.NotificationsAsync(userId, true)) { n.IsRead = true; n.ReadAt = DateTime.UtcNow; }
        await repository.SaveAsync();
    }
}
