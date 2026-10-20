using System.Text.RegularExpressions;
using OrganizationIntranet.Application.Abstractions;
using OrganizationIntranet.Contracts;
using OrganizationIntranet.Domain.Entities;
using AppEntity = OrganizationIntranet.Domain.Entities.Application;

namespace OrganizationIntranet.Application.Services;

public sealed class ApplicationCatalogService(IApplicationCatalogRepository repository)
{
    private static ApplicationDto Map(AppEntity a) => new(a.ApplicationId, a.ApplicationName, a.ApplicationCode, a.BaseUrl,
        a.Description, a.DisplayOrder, a.IsActive, a.Icon, a.RequiresLogin, a.IsInternetAccessible, a.IsPublic, a.AccessRequestNotificationChannels);
    private static bool SafeUrl(string? value) => Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && (uri.Scheme == "http" || uri.Scheme == "https") && string.IsNullOrEmpty(uri.UserInfo);
    private static string Required(string? value, int max, string label)
    {
        value = value?.Trim();
        if (string.IsNullOrEmpty(value) || value.Length > max) throw new ArgumentException($"{label} الزامی است و حداکثر {max} کاراکتر دارد");
        return value;
    }
    private static string? Url(string? value)
    {
        value = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        if (value is not null && (value.Length > 500 || !SafeUrl(value))) throw new ArgumentException("آدرس باید HTTP یا HTTPS و بدون اطلاعات ورود باشد");
        return value;
    }
    private static void Apply(AppEntity entity, ApplicationRequest request)
    {
        entity.ApplicationName = Required(request.ApplicationName, 150, "نام سامانه");
        entity.ApplicationCode = Required(request.ApplicationCode, 50, "کد سامانه").ToUpperInvariant();
        if (!Regex.IsMatch(entity.ApplicationCode, "^[A-Z0-9_-]+$")) throw new ArgumentException("کد سامانه باید شامل حروف انگلیسی، عدد، خط تیره یا زیرخط باشد");
        entity.BaseUrl = Url(request.BaseUrl);
        if (request.Description?.Length > 500) throw new ArgumentException("توضیحات حداکثر ۵۰۰ کاراکتر دارد");
        entity.Description = request.Description?.Trim();
        var icon = string.IsNullOrWhiteSpace(request.Icon) ? "apps" : request.Icon.Trim();
        if (!ApplicationCatalogOptions.Icons.Contains(icon)) throw new ArgumentException("آیکن انتخاب‌شده معتبر نیست");
        entity.Icon = icon;
        entity.DisplayOrder = request.DisplayOrder;
        entity.RequiresLogin = request.RequiresLogin;
        entity.IsInternetAccessible = request.IsInternetAccessible;
        entity.IsPublic = request.IsPublic;
        if (!AccessRequestChannels.Valid(request.AccessRequestNotificationChannels)) throw new ArgumentException("روش اعلان درخواست دسترسی معتبر نیست");
        entity.AccessRequestNotificationChannels = request.AccessRequestNotificationChannels;
    }
    public async Task<List<ApplicationDto>> ListAsync() => (await repository.ListAsync()).Select(Map).ToList();
    public async Task<ApplicationConfigurationDto> GetAsync(int id)
    {
        var a = await repository.FindAsync(id) ?? throw new KeyNotFoundException();
        return new(Map(a), a.AccessGrants.Where(g => g.UserId.HasValue).Select(g => g.UserId!.Value).ToList(),
            a.AccessGrants.Where(g => g.RoleId.HasValue).Select(g => g.RoleId!.Value).ToList(),
            a.Services.OrderBy(s => s.ServiceCode).Select(s => new ApplicationServiceRequest(s.ServiceCode, s.DisplayName, s.EndpointUrl, s.IsActive)).ToList(),
            a.Services.Select(s => new ApplicationServiceSyncDto(s.ServiceCode, s.LastSyncedAt, s.LastSyncError)).ToList());
    }
    public async Task CreateAsync(ApplicationRequest request)
    {
        var a = new AppEntity { CreatedAt = DateTime.UtcNow };
        Apply(a, request); repository.Add(a); await repository.SaveAsync();
    }
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
        Apply(a, request.Application); repository.SetAccess(a, users, roles); repository.SetServices(a, services);
        await repository.SaveAsync();
    }
    public async Task ToggleAsync(int id)
    {
        var a = await repository.FindAsync(id) ?? throw new KeyNotFoundException();
        a.IsActive = !a.IsActive; await repository.SaveAsync();
    }
    public async Task<List<PortalApplicationDto>> AvailableAsync(long userId)
    {
        var apps = await repository.AvailableAsync(userId);
        var counts = await repository.UnreadCountsAsync(userId);
        return apps.Select(a => new PortalApplicationDto(a.ApplicationId, a.ApplicationName, a.Description,
            SafeUrl(a.BaseUrl) ? a.BaseUrl : null, ApplicationCatalogOptions.Icons.Contains(a.Icon) ? a.Icon! : "apps",
            a.RequiresLogin, a.IsInternetAccessible, a.IsPublic, counts.GetValueOrDefault(a.ApplicationId),
            a.Services.Where(s => s.IsActive).OrderBy(s => s.ServiceCode).Select(s => s.ServiceCode).ToList())).ToList();
    }
    public async Task<List<PortalNotificationDto>> NotificationsAsync(long userId) => (await repository.NotificationsAsync(userId, false, 50))
        .Select(n => new PortalNotificationDto(n.UserNotificationId, n.Notification.Application.ApplicationName, n.Notification.Title, n.CreatedAt, !n.IsRead,
            ApplicationLinkPolicy.Destination(n.Notification.Application.BaseUrl, n.Notification.TargetUrl))).ToList();
    public async Task<NotificationDestinationDto> OpenNotificationAsync(long userId, long id)
    {
        var notification = await repository.FindNotificationAsync(userId, id) ?? throw new KeyNotFoundException();
        var destination = ApplicationLinkPolicy.Destination(notification.Notification.Application.BaseUrl, notification.Notification.TargetUrl)
            ?? throw new ArgumentException("آدرس مقصد اعلان معتبر نیست؛ با مدیر سامانه تماس بگیرید");
        if (!notification.IsRead) { notification.IsRead = true; notification.ReadAt = DateTime.UtcNow; await repository.SaveAsync(); }
        return new(destination);
    }
    public async Task MarkNotificationsReadAsync(long userId)
    {
        foreach (var n in await repository.NotificationsAsync(userId, true)) { n.IsRead = true; n.ReadAt = DateTime.UtcNow; }
        await repository.SaveAsync();
    }
}
