using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrganizationIntranet.Application.Services;
using OrganizationIntranet.Contracts;

namespace OrganizationIntranet.Api.Controllers;

[ApiController, Route("api/admin"), Authorize(Roles = "ADMIN")]
public sealed class AdminController(IntranetService service) : ControllerBase
{
    [HttpGet("dashboard")] public Task<DashboardDto> Dashboard() => service.DashboardAsync();
    [HttpGet("users")] public Task<List<UserDto>> Users() => service.UsersAsync();
    [HttpGet("users/{id:long}")] public async Task<IActionResult> UserById(long id) { var user = await service.UserAsync(id); return user is null ? NotFound() : Ok(user); }
    [HttpPost("users")] public async Task<OperationResult> SaveUser(UserEditRequest request) { await service.SaveUserAsync(request); return new(true, "کاربر با موفقیت ذخیره شد"); }
    [HttpGet("roles")] public Task<List<RoleDto>> Roles() => service.RolesAsync();
    [HttpPost("roles")] public async Task<OperationResult> CreateRole(RoleRequest request) { await service.CreateRoleAsync(request); return new(true, "نقش جدید ایجاد شد"); }
    [HttpPost("roles/{id:int}/toggle")] public async Task<OperationResult> ToggleRole(int id) { await service.ToggleRoleAsync(id); return new(true); }
    [HttpGet("applications")] public Task<List<ApplicationDto>> Applications() => service.ApplicationsAsync();
    [HttpPost("applications")] public async Task<OperationResult> CreateApplication(ApplicationRequest request) { await service.CreateApplicationAsync(request); return new(true, "سامانه جدید ایجاد شد"); }
    [HttpPost("applications/{id:int}/toggle")] public async Task<OperationResult> ToggleApplication(int id) { await service.ToggleApplicationAsync(id); return new(true); }
    [HttpGet("permissions")] public Task<List<PermissionDto>> Permissions() => service.PermissionsAsync();
    [HttpGet("permissions/{id:int}")] public async Task<IActionResult> Permission(int id) { var permission = await service.PermissionAsync(id); return permission is null ? NotFound() : Ok(permission); }
    [HttpPost("permissions")] public async Task<OperationResult> CreatePermission(PermissionRequest request) { await service.CreatePermissionAsync(request); return new(true, "مجوز جدید ایجاد شد"); }
    [HttpPost("permissions/{id:int}/toggle")] public async Task<OperationResult> TogglePermission(int id) { await service.TogglePermissionAsync(id); return new(true); }
    [HttpPost("permissions/{id:int}/roles")] public async Task<OperationResult> AssignPermission(int id, RolesRequest request) { await service.AssignPermissionAsync(id, request); return new(true, "نقش‌های مجوز به‌روزرسانی شد"); }
}
