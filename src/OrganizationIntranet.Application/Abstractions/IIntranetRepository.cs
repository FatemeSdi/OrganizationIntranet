using OrganizationIntranet.Domain.Entities;
using AppEntity = OrganizationIntranet.Domain.Entities.Application;

namespace OrganizationIntranet.Application.Abstractions;

// Returned entities are tracked for the lifetime of one API request.
/// <summary>دسترسی به داده‌ی کاربران، نقش‌ها و مجوزها؛ پیاده‌سازی در لایه‌ی API.</summary>
public interface IIntranetRepository
{
    /// <summary>کاربر با شناسه؛ همراه نقش‌ها و مجوزها.</summary>
    Task<User?> FindUserAsync(long id);
    /// <summary>کاربر با نام کاربری.</summary>
    Task<User?> FindUserAsync(string username);
    /// <summary>حداکثر دو کاربر با این کد ملی؛ بیش از یکی یعنی داده‌ی مبهم و ورود با کد ملی رد می‌شود.</summary>
    Task<List<User>> FindUsersByNationalIdAsync(string nationalId);
    /// <summary>همه‌ی کاربران.</summary>
    Task<List<User>> GetUsersAsync();
    /// <summary>همه‌ی نقش‌ها.</summary>
    Task<List<Role>> GetRolesAsync();
    /// <summary>نقش با کاربران (UserRoles.User) و مجوزها (RolePermissions.Permission.Application).</summary>
    Task<Role?> FindRoleAsync(int id);
    /// <summary>شمار کاربران، مجوزها و اتصال‌های دیگر (دسترسی سامانه، گروه نظارت) هر نقش.</summary>
    Task<Dictionary<int, RoleUsage>> GetRoleUsageAsync();
    /// <summary>همه‌ی سامانه‌ها.</summary>
    Task<List<AppEntity>> GetApplicationsAsync();
    /// <summary>همه‌ی مجوزها با سامانه و نقش‌های دارنده (فقط‌خواندنی).</summary>
    Task<List<Permission>> GetPermissionsAsync();
    /// <summary>یک مجوز با سامانه و نقش‌های دارنده.</summary>
    Task<Permission?> FindPermissionAsync(int id);
    /// <summary>آیا واحد سازمانی با این شناسه وجود دارد و فعال است؟</summary>
    Task<bool> ActiveUnitExistsAsync(int unitId);
    /// <summary>کاربر تازه را اضافه می‌کند.</summary>
    void AddUser(User user);
    /// <summary>نقش تازه را اضافه می‌کند.</summary>
    void AddRole(Role role);
    /// <summary>نقش را حذف می‌کند (فقط نقش بدون استفاده).</summary>
    void RemoveRole(Role role);
    /// <summary>مجوز تازه را اضافه می‌کند.</summary>
    void AddPermission(Permission permission);
    /// <summary>نقش‌های کاربر را با مجموعه‌ی داده‌شده یکی می‌کند (اضافه و حذف فقط تفاوت‌ها).</summary>
    void SetUserRoles(User user, IReadOnlyCollection<int> ids);
    /// <summary>نقش‌های دارنده‌ی مجوز را با مجموعه‌ی داده‌شده یکی می‌کند.</summary>
    void SetPermissionRoles(Permission permission, IReadOnlyCollection<int> ids);
    /// <summary>تغییرات را در یک تراکنش ذخیره می‌کند.</summary>
    Task SaveAsync();
}

/// <summary>شمار کاربران، مجوزها و اتصال‌های هر نقش؛ نقش فقط وقتی هر سه صفر باشند قابل حذف است.</summary>
public sealed record RoleUsage(int Users, int Permissions, int Links);

/// <summary>هش و بررسی رمز کاربر (ASP.NET PasswordHasher).</summary>
public interface IPasswordService
{
    string Hash(User user, string password);
    bool Verify(User user, string password);
}
