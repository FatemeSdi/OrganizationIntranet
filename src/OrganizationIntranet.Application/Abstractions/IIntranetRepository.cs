using OrganizationIntranet.Domain.Entities;
using AppEntity = OrganizationIntranet.Domain.Entities.Application;

namespace OrganizationIntranet.Application.Abstractions;

// Returned entities are tracked for the lifetime of one API request.
public interface IIntranetRepository
{
    Task<User?> FindUserAsync(long id);
    Task<User?> FindUserAsync(string username);
    /// <summary>حداکثر دو کاربر با این کد ملی؛ بیش از یکی یعنی داده‌ی مبهم و ورود با کد ملی رد می‌شود.</summary>
    Task<List<User>> FindUsersByNationalIdAsync(string nationalId);
    Task<List<User>> GetUsersAsync();
    Task<List<Role>> GetRolesAsync();
    /// <summary>نقش با کاربران (UserRoles.User) و مجوزها (RolePermissions.Permission.Application).</summary>
    Task<Role?> FindRoleAsync(int id);
    /// <summary>شمار کاربران، مجوزها و اتصال‌های دیگر (دسترسی سامانه، گروه نظارت) هر نقش.</summary>
    Task<Dictionary<int, RoleUsage>> GetRoleUsageAsync();
    Task<List<AppEntity>> GetApplicationsAsync();
    Task<List<Permission>> GetPermissionsAsync();
    Task<Permission?> FindPermissionAsync(int id);
    void AddUser(User user);
    void AddRole(Role role);
    void RemoveRole(Role role);
    void AddPermission(Permission permission);
    void SetUserRoles(User user, IReadOnlyCollection<int> ids);
    void SetPermissionRoles(Permission permission, IReadOnlyCollection<int> ids);
    Task SaveAsync();
}

public sealed record RoleUsage(int Users, int Permissions, int Links);

public interface IPasswordService
{
    string Hash(User user, string password);
    bool Verify(User user, string password);
}
