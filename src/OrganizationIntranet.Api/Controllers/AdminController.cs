using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrganizationIntranet.Application.Services;
using OrganizationIntranet.Contracts;
using P = OrganizationIntranet.Contracts.AdminPermissions;

namespace OrganizationIntranet.Api.Controllers;

// ورود به پنل (INTRANET_ADMIN) برای همه؛ هر عملیات مجوز دقیق خودش را هم لازم دارد (بررسی زنده از بانک).
// فهرست نقش‌ها و سامانه‌ها برای فرم‌های دیگر (کاربر، مجوز) لازم است و فقط ورود به پنل را می‌خواهد.
/// <summary>API پنل مدیریت: کاربران، نقش‌ها، مجوزها و سامانه‌ها. فقط از Admin (با کلید مدیریتی) و برای دارندگان INTRANET_ADMIN قابل فراخوانی است و هر عملیات مجوز دقیق خودش را هم زنده از بانک می‌خواهد.</summary>
[OrganizationIntranet.Api.Security.AdminClient]
[ApiController, Route("api/admin"), Authorize(Policy = P.IntranetAdminPolicy)]
public sealed class AdminController(IntranetService service, ApplicationCatalogService catalog, SsoService sso, OrganizationIntranet.Api.Security.LivePermissions permissions, IConfiguration configuration, ILogger<AdminController> logger) : ControllerBase
{
    /// <summary>شناسه‌ی مدیر انجام‌دهنده‌ی عملیات؛ برای ثبت در لاگ.</summary>
    private long Actor => long.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
    /// <summary>آمار داشبورد: شمار کاربران، نقش‌ها و سامانه‌ها؛ فهرست آخرین کاربران فقط برای دارنده‌ی USER_VIEW.</summary>
    [HttpGet("dashboard")] public async Task<DashboardDto> Dashboard()
    {
        var data = await service.DashboardAsync();
        return (await permissions.ForAsync(User)).Contains(P.UserView) ? data : data with { RecentUsers = [] };
    }
    /// <summary>فهرست همه‌ی کاربران (نیاز به USER_VIEW).</summary>
    [HttpGet("users"), Authorize(Policy = P.UserViewPolicy)] public Task<List<UserDto>> Users() => service.UsersAsync();
    /// <summary>جزئیات یک کاربر با نقش‌هایش؛ اگر نبود ۴۰۴.</summary>
    [HttpGet("users/{id:long}"), Authorize(Policy = P.UserViewPolicy)] public async Task<IActionResult> UserById(long id) { var user = await service.UserAsync(id); return user is null ? NotFound() : Ok(user); }
    /// <summary>ساخت یا ویرایش کاربر (با شناسه‌ی خالی = جدید)؛ شامل نقش‌ها و رمز. نیاز به USER_EDIT.</summary>
    [HttpPost("users"), Authorize(Policy = P.UserEditPolicy)] public async Task<OperationResult> SaveUser(UserEditRequest request)
    {
        await service.SaveUserAsync(request);
        logger.LogInformation("User {UserId} saved by {ActorId}", request.Id, Actor);
        return new(true, "کاربر با موفقیت ذخیره شد");
    }
    /// <summary>فهرست سامانه‌های دارای ورود یکپارچه با نام کاربری نگاشت‌شده‌ی این کاربر در هر کدام (docs/sso.md)؛ نیاز به USER_VIEW.</summary>
    [HttpGet("users/{id:long}/sso-accounts"), Authorize(Policy = P.UserViewPolicy)] public Task<SsoAccountsDto> SsoAccounts(long id) => sso.AccountsAsync(id);
    /// <summary>ثبت یا (با مقدار خالی) حذف نام کاربری این کاربر در یک سامانه؛ هر حساب سامانه فقط به یک کاربر اینترانت تعلق می‌گیرد. نیاز به USER_EDIT.</summary>
    [HttpPost("users/{id:long}/sso-accounts/{applicationId:int}"), Authorize(Policy = P.UserEditPolicy)]
    public async Task<SsoAccountsDto> SaveSsoAccount(long id, int applicationId, SsoAccountRequest request)
    {
        var result = await sso.SetAccountAsync(id, applicationId, request.ExternalUsername, Actor);
        logger.LogInformation("SSO account name of user {UserId} for application {ApplicationId} changed by {ActorId}", id, applicationId, Actor);
        return result;
    }
    /// <summary>افزودن یا برداشتن یک نقش از کاربر موجود (ثبت لحظه‌ای)؛ همان قواعد ذخیره‌ی کاربر، از جمله باقی‌ماندن حداقل یک مدیر فعال.</summary>
    [HttpPost("users/{id:long}/role"), Authorize(Policy = P.UserEditPolicy)]
    public async Task<OperationResult> ToggleUserRole(long id, UserRoleToggleRequest request)
    {
        await service.SetUserRoleAsync(id, request);
        logger.LogInformation("User {UserId} role {RoleId} granted={Granted} by {ActorId}", id, request.RoleId, request.Granted, Actor);
        return new(true, request.Granted ? "نقش به کاربر داده شد" : "نقش از کاربر برداشته شد");
    }
    /// <summary>فهرست نقش‌ها با شمار کاربران و مجوزهای هر نقش؛ برای فرم‌های دیگر هم لازم است و فقط ورود به پنل می‌خواهد.</summary>
    [HttpGet("roles")] public Task<List<RoleDto>> Roles() => service.RolesAsync();
    /// <summary>جزئیات یک نقش: اعضا (فقط با USER_VIEW) و مجوزها.</summary>
    [HttpGet("roles/{id:int}"), Authorize(Policy = P.RoleViewPolicy)] public async Task<IActionResult> RoleById(int id)
    {
        var role = await service.RoleAsync(id, (await permissions.ForAsync(User)).Contains(P.UserView));
        return role is null ? NotFound() : Ok(role);
    }
    /// <summary>ساخت نقش تازه؛ کد باید یکتا و فقط حروف بزرگ انگلیسی، رقم و _ باشد.</summary>
    [HttpPost("roles"), Authorize(Policy = P.RoleEditPolicy)] public async Task<OperationResult> CreateRole(RoleRequest request)
    {
        await service.CreateRoleAsync(request);
        logger.LogInformation("Role {RoleCode} created by {ActorId}", request.RoleCode, Actor);
        return new(true, "نقش جدید ایجاد شد");
    }
    /// <summary>ویرایش نام و کد نقش (نقش‌های سیستمی ADMIN و EXPERT کدشان عوض نمی‌شود).</summary>
    [HttpPost("roles/{id:int}"), Authorize(Policy = P.RoleEditPolicy)] public async Task<OperationResult> UpdateRole(int id, RoleRequest request)
    {
        await service.UpdateRoleAsync(id, request);
        logger.LogInformation("Role {RoleId} updated by {ActorId}", id, Actor);
        return new(true, "نقش ذخیره شد");
    }
    /// <summary>حذف نقش؛ فقط نقش غیرسیستمی و بدون کاربر، مجوز، دسترسی سامانه یا عضویت گروه نظارت.</summary>
    [HttpPost("roles/{id:int}/delete"), Authorize(Policy = P.RoleEditPolicy)] public async Task<OperationResult> DeleteRole(int id)
    {
        await service.DeleteRoleAsync(id);
        logger.LogInformation("Role {RoleId} deleted by {ActorId}", id, Actor);
        return new(true, "نقش حذف شد");
    }
    /// <summary>فعال/غیرفعال کردن نقش؛ نقش ADMIN غیرفعال نمی‌شود.</summary>
    [HttpPost("roles/{id:int}/toggle"), Authorize(Policy = P.RoleEditPolicy)] public async Task<OperationResult> ToggleRole(int id)
    {
        await service.ToggleRoleAsync(id);
        logger.LogInformation("Role {RoleId} toggled by {ActorId}", id, Actor);
        return new(true);
    }
    /// <summary>فهرست همه‌ی سامانه‌های کاتالوگ (برای فرم‌ها).</summary>
    [HttpGet("applications")] public Task<List<ApplicationDto>> Applications() => catalog.ListAsync();
    /// <summary>ثبت سامانه‌ی تازه. «نشانی ورود» الزامی است و «ترتیب نمایش» اگر خالی باشد سامانه آخرین ردیف می‌شود.</summary>
    [HttpPost("applications"), Authorize(Policy = P.ApplicationEditPolicy)] public async Task<OperationResult> CreateApplication(ApplicationRequest request) { await catalog.CreateAsync(request); return new(true, "سامانه جدید ایجاد شد"); }
    /// <summary>تنظیمات ورود یکپارچه‌ی یک سامانه (مسیر و وضعیت کلید؛ خود کلید هرگز برنمی‌گردد).</summary>
    [HttpGet("applications/{id:int}/sso"), Authorize(Policy = P.ApplicationViewPolicy)] public Task<SsoSettingsDto> SsoSettings(int id) => sso.SettingsAsync(id);
    /// <summary>ذخیره‌ی مسیر SSO و ساخت یا ثبت کلید آن. کلید ساخته‌شده فقط در همین پاسخ می‌آید. کلید نباید با کلیدهای داخلی UI یا کلید اعلان همین سامانه یکی باشد.</summary>
    [HttpPost("applications/{id:int}/sso"), Authorize(Policy = P.ApplicationEditPolicy)]
    public async Task<SsoSettingsResultDto> SaveSsoSettings(int id, SsoSettingsRequest request)
    {
        var code = (await sso.SettingsAsync(id)).ApplicationCode.ToUpperInvariant();
        var forbidden = new[] { configuration["Api:ClientKey"], configuration["Api:AdminClientKey"], configuration[$"ApplicationIntegrations:{code}:PushKey"], configuration[$"ApplicationIntegrations:{code}:PullKey"] }
            .Where(k => !string.IsNullOrEmpty(k)).Select(k => k!).ToArray();
        var result = await sso.SaveSettingsAsync(id, request, forbidden);
        logger.LogInformation("SSO settings of application {ApplicationId} changed by {ActorId} (key action: {KeyAction})", id, Actor, request.KeyAction ?? SsoKeyActions.Keep);
        return result;
    }
    /// <summary>فعال/غیرفعال کردن سامانه در پورتال.</summary>
    [HttpPost("applications/{id:int}/toggle"), Authorize(Policy = P.ApplicationEditPolicy)] public async Task<OperationResult> ToggleApplication(int id) { await catalog.ToggleAsync(id); return new(true); }
    /// <summary>افزودن یا برداشتن یک کاربر یا نقش از دسترسی سامانه (ثبت لحظه‌ای)، بدون ذخیره‌ی بقیه‌ی تنظیمات.</summary>
    [HttpPost("applications/{id:int}/access"), Authorize(Policy = P.ApplicationEditPolicy)]
    public async Task<OperationResult> ToggleApplicationAccess(int id, AccessToggleRequest request)
    {
        await catalog.SetAccessAsync(id, request);
        logger.LogInformation("Application {ApplicationId} access user={UserId} role={RoleId} granted={Granted} by {ActorId}", id, request.UserId, request.RoleId, request.Granted, Actor);
        return new(true, request.Granted ? "دسترسی ثبت شد" : "دسترسی برداشته شد");
    }
    /// <summary>تنظیمات کامل یک سامانه: مشخصات، دسترسی کاربران و نقش‌ها، سرویس‌ها و وضعیت همگام‌سازی.</summary>
    [HttpGet("applications/{id:int}"), Authorize(Policy = P.ApplicationViewPolicy)] public Task<ApplicationConfigurationDto> Application(int id) => catalog.GetAsync(id);
    /// <summary>ذخیره‌ی کامل تنظیمات سامانه (مشخصات، دسترسی‌ها و سرویس‌ها) در یک تراکنش.</summary>
    [HttpPost("applications/{id:int}"), Authorize(Policy = P.ApplicationEditPolicy)]
    public async Task<OperationResult> SaveApplication(int id, ApplicationConfigurationRequest request)
    {
        await catalog.SaveAsync(id, request);
        logger.LogInformation("Application {ApplicationId} configuration saved by {UserId}", id, Actor);
        return new(true, "تنظیمات و دسترسی‌های سامانه ذخیره شد");
    }
    /// <summary>فهرست مجوزها با شمار نقش‌های دارنده.</summary>
    [HttpGet("permissions"), Authorize(Policy = P.PermissionViewPolicy)] public Task<List<PermissionDto>> Permissions() => service.PermissionsAsync();
    /// <summary>یک مجوز با شناسه‌ی نقش‌های دارنده‌اش؛ اگر نبود ۴۰۴.</summary>
    [HttpGet("permissions/{id:int}"), Authorize(Policy = P.PermissionViewPolicy)] public async Task<IActionResult> Permission(int id) { var permission = await service.PermissionAsync(id); return permission is null ? NotFound() : Ok(permission); }
    /// <summary>ساخت مجوز تازه برای یک سامانه (کد یکتا در همان سامانه).</summary>
    [HttpPost("permissions"), Authorize(Policy = P.PermissionEditPolicy)] public async Task<OperationResult> CreatePermission(PermissionRequest request) { await service.CreatePermissionAsync(request); return new(true, "مجوز جدید ایجاد شد"); }
    /// <summary>فعال/غیرفعال کردن مجوز؛ مجوز سیستمی پنل غیرفعال نمی‌شود.</summary>
    [HttpPost("permissions/{id:int}/toggle"), Authorize(Policy = P.PermissionEditPolicy)] public async Task<OperationResult> TogglePermission(int id) { await service.TogglePermissionAsync(id); return new(true); }
    /// <summary>تعیین مجموعه‌ی نقش‌های دارای یک مجوز؛ مجوز سیستمی از نقش ADMIN گرفته نمی‌شود.</summary>
    [HttpPost("permissions/{id:int}/roles"), Authorize(Policy = P.PermissionEditPolicy)] public async Task<OperationResult> AssignPermission(int id, RolesRequest request)
    {
        await service.AssignPermissionAsync(id, request);
        logger.LogInformation("Permission {PermissionId} roles changed by {ActorId}", id, Actor);
        return new(true, "نقش‌های مجوز به‌روزرسانی شد");
    }
}
