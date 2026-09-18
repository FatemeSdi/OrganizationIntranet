using OrganizationIntranet.Application.Abstractions;
using OrganizationIntranet.Contracts;
using OrganizationIntranet.Domain.Entities;
using AppEntity = OrganizationIntranet.Domain.Entities.Application;

namespace OrganizationIntranet.Application.Services;

public sealed class IntranetService(IIntranetRepository repository, IPasswordService passwords)
{
    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
    private static void Required(string? value, int length, string name)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > length)
            throw new ArgumentException($"{name} الزامی است و حداکثر {length} کاراکتر دارد");
    }
    private static void Limit(string? value, int length)
    {
        if (value?.Length > length) throw new ArgumentException("طول مقدار واردشده بیش از حد مجاز است");
    }
    private static UserDto Map(User u) => new(u.UserId, u.Name, u.LastName, u.Username, u.NationalId, u.MobileNumber, u.Email, u.IsActive, u.CreatedAt, u.LastLogin, string.Join("، ", u.UserRoles.Select(r => r.Role.RoleName)), u.UserRoles.Select(r => r.RoleId).ToList());
    private static ProfileDto Profile(User u) => new(u.UserId, u.Username, u.Name, u.LastName, u.NationalId, u.MobileNumber, u.Email, u.CreatedAt, u.LastLogin,
        u.UserRoles.Count > 0 ? string.Join("، ", u.UserRoles.Select(r => r.Role.RoleName)) : "بدون نقش",
        u.UserRoles.Where(r => r.Role.IsActive).Select(r => r.Role.RoleCode).ToList(),
        u.UserRoles.Where(r => r.Role.IsActive).SelectMany(r => r.Role.RolePermissions).Where(r => r.Permission.IsActive).Select(r => r.Permission.PermissionCode).Distinct().ToList());

    public async Task<ProfileDto?> ProfileAsync(long id)
    {
        var user = await repository.FindUserAsync(id);
        return user is null ? null : Profile(user);
    }
    public async Task<List<UserDto>> UsersAsync() => (await repository.GetUsersAsync()).OrderBy(u => u.Name).Select(Map).ToList();
    public async Task<UserDto?> UserAsync(long id)
    {
        var user = await repository.FindUserAsync(id);
        return user is null ? null : Map(user);
    }
    public async Task<DashboardDto> DashboardAsync()
    {
        var users = await repository.GetUsersAsync();
        return new(users.Count, users.Count(u => u.IsActive), (await repository.GetRolesAsync()).Count, (await repository.GetApplicationsAsync()).Count, users.OrderByDescending(u => u.CreatedAt).Take(5).Select(Map).ToList());
    }
    public async Task SaveUserAsync(UserEditRequest request)
    {
        Required(request.Name, 100, "نام"); Required(request.LastName, 100, "نام خانوادگی"); Required(request.Username, 100, "نام کاربری");
        Limit(request.NationalId, 10); Limit(request.MobileNumber, 20); Limit(request.Email, 200); Limit(request.NewPassword, 1024);
        var ids = request.SelectedRoleIds.Distinct().ToList();
        await ValidateRoles(ids);
        bool isNew = request.Id is null or 0;
        var user = isNew ? new User { CreatedAt = DateTime.UtcNow } : await repository.FindUserAsync(request.Id!.Value) ?? throw new KeyNotFoundException();
        if (isNew) { Required(request.NewPassword, 1024, "رمز عبور"); repository.AddUser(user); }
        user.Name = request.Name; user.LastName = request.LastName; user.Username = request.Username;
        user.NationalId = Optional(request.NationalId); user.MobileNumber = Optional(request.MobileNumber); user.Email = Optional(request.Email); user.IsActive = request.IsActive;
        if (!string.IsNullOrWhiteSpace(request.NewPassword))
        {
            AuthenticationService.ValidateNewPassword(request.NewPassword, request.NewPassword);
            user.PasswordHash = passwords.Hash(user, request.NewPassword);
        }
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        user.AuthenticationVersion = Guid.NewGuid();
        repository.SetUserRoles(user, ids);
        await repository.SaveAsync(); // One SaveChanges transaction also covers a new user's role assignments.
    }
    public async Task<List<RoleDto>> RolesAsync() => (await repository.GetRolesAsync()).OrderBy(r => r.RoleName).Select(r => new RoleDto(r.RoleId, r.RoleName, r.RoleCode, r.IsActive, r.CreatedAt)).ToList();
    public async Task CreateRoleAsync(RoleRequest request)
    {
        Required(request.RoleName, 100, "نام نقش"); Required(request.RoleCode, 50, "کد نقش");
        repository.AddRole(new Role { RoleName = request.RoleName, RoleCode = request.RoleCode.ToUpperInvariant(), IsActive = true, CreatedAt = DateTime.UtcNow });
        await repository.SaveAsync();
    }
    public async Task ToggleRoleAsync(int id)
    {
        var entity = (await repository.GetRolesAsync()).FirstOrDefault(r => r.RoleId == id) ?? throw new KeyNotFoundException();
        entity.IsActive = !entity.IsActive; await repository.SaveAsync();
    }
    public async Task<List<ApplicationDto>> ApplicationsAsync() => (await repository.GetApplicationsAsync()).OrderBy(a => a.DisplayOrder).Select(a => new ApplicationDto(a.ApplicationId, a.ApplicationName, a.ApplicationCode, a.BaseUrl, a.Description, a.DisplayOrder, a.IsActive)).ToList();
    public async Task CreateApplicationAsync(ApplicationRequest request)
    {
        Required(request.ApplicationName, 150, "نام سامانه"); Required(request.ApplicationCode, 50, "کد سامانه"); Limit(request.BaseUrl, 500); Limit(request.Description, 500);
        if (!string.IsNullOrWhiteSpace(request.BaseUrl) && (!Uri.TryCreate(request.BaseUrl, UriKind.Absolute, out var url) || (url.Scheme != "http" && url.Scheme != "https"))) throw new ArgumentException("آدرس سامانه باید HTTP یا HTTPS باشد");
        repository.AddApplication(new AppEntity { ApplicationName = request.ApplicationName, ApplicationCode = request.ApplicationCode.ToUpperInvariant(), BaseUrl = Optional(request.BaseUrl), Description = Optional(request.Description), DisplayOrder = request.DisplayOrder, IsActive = true, CreatedAt = DateTime.UtcNow });
        await repository.SaveAsync();
    }
    public async Task ToggleApplicationAsync(int id)
    {
        var entity = (await repository.GetApplicationsAsync()).FirstOrDefault(a => a.ApplicationId == id) ?? throw new KeyNotFoundException();
        entity.IsActive = !entity.IsActive; await repository.SaveAsync();
    }
    private static PermissionDto Map(Permission p) => new(p.PermissionId, p.PermissionName, p.PermissionCode, p.Description, p.Application.ApplicationName, p.IsActive, p.RolePermissions.Count, p.RolePermissions.Select(r => r.RoleId).ToList());
    public async Task<List<PermissionDto>> PermissionsAsync() => (await repository.GetPermissionsAsync()).OrderBy(p => p.Application.ApplicationName).ThenBy(p => p.PermissionName).Select(Map).ToList();
    public async Task<PermissionDto?> PermissionAsync(int id)
    {
        var p = await repository.FindPermissionAsync(id); return p is null ? null : Map(p);
    }
    public async Task CreatePermissionAsync(PermissionRequest request)
    {
        Required(request.PermissionName, 150, "نام مجوز"); Required(request.PermissionCode, 100, "کد مجوز"); Limit(request.Description, 500);
        if (!(await repository.GetApplicationsAsync()).Any(a => a.ApplicationId == request.ApplicationId && a.IsActive)) throw new ArgumentException("سامانه انتخاب‌شده معتبر نیست");
        repository.AddPermission(new Permission { PermissionName = request.PermissionName, PermissionCode = request.PermissionCode.ToUpperInvariant(), ApplicationId = request.ApplicationId, Description = Optional(request.Description), IsActive = true, CreatedAt = DateTime.UtcNow });
        await repository.SaveAsync();
    }
    public async Task TogglePermissionAsync(int id)
    {
        var p = await repository.FindPermissionAsync(id) ?? throw new KeyNotFoundException(); p.IsActive = !p.IsActive; await repository.SaveAsync();
    }
    public async Task AssignPermissionAsync(int id, RolesRequest request)
    {
        var p = await repository.FindPermissionAsync(id) ?? throw new KeyNotFoundException();
        var ids = request.SelectedRoleIds.Distinct().ToList(); await ValidateRoles(ids);
        repository.SetPermissionRoles(p, ids); await repository.SaveAsync();
    }
    private async Task ValidateRoles(List<int> ids)
    {
        var roles = await repository.GetRolesAsync();
        if (ids.Any(id => !roles.Any(r => r.RoleId == id && r.IsActive))) throw new ArgumentException("نقش انتخاب‌شده معتبر یا فعال نیست");
    }
}
