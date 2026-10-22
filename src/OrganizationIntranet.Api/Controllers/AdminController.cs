using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrganizationIntranet.Application.Services;
using OrganizationIntranet.Contracts;
using P = OrganizationIntranet.Contracts.AdminPermissions;

namespace OrganizationIntranet.Api.Controllers;

// ورود به پنل (INTRANET_ADMIN) برای همه؛ هر عملیات مجوز دقیق خودش را هم لازم دارد (بررسی زنده از بانک).
// فهرست نقش‌ها و سامانه‌ها برای فرم‌های دیگر (کاربر، مجوز) لازم است و فقط ورود به پنل را می‌خواهد.
[OrganizationIntranet.Api.Security.AdminClient]
[ApiController, Route("api/admin"), Authorize(Policy = P.IntranetAdminPolicy)]
public sealed class AdminController(IntranetService service, ApplicationCatalogService catalog, OrganizationIntranet.Api.Security.LivePermissions permissions, ILogger<AdminController> logger) : ControllerBase
{
    private long Actor => long.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
    // آمار کلی برای همه‌ی واردان پنل؛ فهرست آخرین کاربران فقط با مجوز مشاهده‌ی کاربران.
    [HttpGet("dashboard")] public async Task<DashboardDto> Dashboard()
    {
        var data = await service.DashboardAsync();
        return (await permissions.ForAsync(User)).Contains(P.UserView) ? data : data with { RecentUsers = [] };
    }
    [HttpGet("users"), Authorize(Policy = P.UserViewPolicy)] public Task<List<UserDto>> Users() => service.UsersAsync();
    [HttpGet("users/{id:long}"), Authorize(Policy = P.UserViewPolicy)] public async Task<IActionResult> UserById(long id) { var user = await service.UserAsync(id); return user is null ? NotFound() : Ok(user); }
    [HttpPost("users"), Authorize(Policy = P.UserEditPolicy)] public async Task<OperationResult> SaveUser(UserEditRequest request)
    {
        await service.SaveUserAsync(request);
        logger.LogInformation("User {UserId} saved by {ActorId}", request.Id, Actor);
        return new(true, "کاربر با موفقیت ذخیره شد");
    }
    [HttpGet("roles")] public Task<List<RoleDto>> Roles() => service.RolesAsync();
    // جزئیات نقش با مجوز مشاهده‌ی نقش‌ها؛ فهرست اعضا فقط برای دارنده‌ی USER_VIEW (مثل داشبورد).
    [HttpGet("roles/{id:int}"), Authorize(Policy = P.RoleViewPolicy)] public async Task<IActionResult> RoleById(int id)
    {
        var role = await service.RoleAsync(id, (await permissions.ForAsync(User)).Contains(P.UserView));
        return role is null ? NotFound() : Ok(role);
    }
    [HttpPost("roles"), Authorize(Policy = P.RoleEditPolicy)] public async Task<OperationResult> CreateRole(RoleRequest request)
    {
        await service.CreateRoleAsync(request);
        logger.LogInformation("Role {RoleCode} created by {ActorId}", request.RoleCode, Actor);
        return new(true, "نقش جدید ایجاد شد");
    }
    [HttpPost("roles/{id:int}"), Authorize(Policy = P.RoleEditPolicy)] public async Task<OperationResult> UpdateRole(int id, RoleRequest request)
    {
        await service.UpdateRoleAsync(id, request);
        logger.LogInformation("Role {RoleId} updated by {ActorId}", id, Actor);
        return new(true, "نقش ذخیره شد");
    }
    [HttpPost("roles/{id:int}/delete"), Authorize(Policy = P.RoleEditPolicy)] public async Task<OperationResult> DeleteRole(int id)
    {
        await service.DeleteRoleAsync(id);
        logger.LogInformation("Role {RoleId} deleted by {ActorId}", id, Actor);
        return new(true, "نقش حذف شد");
    }
    [HttpPost("roles/{id:int}/toggle"), Authorize(Policy = P.RoleEditPolicy)] public async Task<OperationResult> ToggleRole(int id)
    {
        await service.ToggleRoleAsync(id);
        logger.LogInformation("Role {RoleId} toggled by {ActorId}", id, Actor);
        return new(true);
    }
    [HttpGet("applications")] public Task<List<ApplicationDto>> Applications() => catalog.ListAsync();
    [HttpPost("applications"), Authorize(Policy = P.ApplicationEditPolicy)] public async Task<OperationResult> CreateApplication(ApplicationRequest request) { await catalog.CreateAsync(request); return new(true, "سامانه جدید ایجاد شد"); }
    [HttpPost("applications/{id:int}/toggle"), Authorize(Policy = P.ApplicationEditPolicy)] public async Task<OperationResult> ToggleApplication(int id) { await catalog.ToggleAsync(id); return new(true); }
    [HttpGet("applications/{id:int}"), Authorize(Policy = P.ApplicationViewPolicy)] public Task<ApplicationConfigurationDto> Application(int id) => catalog.GetAsync(id);
    [HttpPost("applications/{id:int}"), Authorize(Policy = P.ApplicationEditPolicy)]
    public async Task<OperationResult> SaveApplication(int id, ApplicationConfigurationRequest request)
    {
        await catalog.SaveAsync(id, request);
        logger.LogInformation("Application {ApplicationId} configuration saved by {UserId}", id, Actor);
        return new(true, "تنظیمات و دسترسی‌های سامانه ذخیره شد");
    }
    [HttpGet("permissions"), Authorize(Policy = P.PermissionViewPolicy)] public Task<List<PermissionDto>> Permissions() => service.PermissionsAsync();
    [HttpGet("permissions/{id:int}"), Authorize(Policy = P.PermissionViewPolicy)] public async Task<IActionResult> Permission(int id) { var permission = await service.PermissionAsync(id); return permission is null ? NotFound() : Ok(permission); }
    [HttpPost("permissions"), Authorize(Policy = P.PermissionEditPolicy)] public async Task<OperationResult> CreatePermission(PermissionRequest request) { await service.CreatePermissionAsync(request); return new(true, "مجوز جدید ایجاد شد"); }
    [HttpPost("permissions/{id:int}/toggle"), Authorize(Policy = P.PermissionEditPolicy)] public async Task<OperationResult> TogglePermission(int id) { await service.TogglePermissionAsync(id); return new(true); }
    [HttpPost("permissions/{id:int}/roles"), Authorize(Policy = P.PermissionEditPolicy)] public async Task<OperationResult> AssignPermission(int id, RolesRequest request)
    {
        await service.AssignPermissionAsync(id, request);
        logger.LogInformation("Permission {PermissionId} roles changed by {ActorId}", id, Actor);
        return new(true, "نقش‌های مجوز به‌روزرسانی شد");
    }
}
