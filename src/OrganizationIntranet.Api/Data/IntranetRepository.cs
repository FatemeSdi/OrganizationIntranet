using Microsoft.EntityFrameworkCore;
using OrganizationIntranet.Application.Abstractions;
using OrganizationIntranet.Domain.Entities;
using AppEntity = OrganizationIntranet.Domain.Entities.Application;

namespace OrganizationIntranet.Api.Data;

/// <summary>پیاده‌سازی EF Core داده‌ی کاربران، نقش‌ها و مجوزها. موجودیت‌های برگشتی تا پایان همان درخواست ردیابی می‌شوند.</summary>
public sealed class IntranetRepository(AppDbContext db) : IIntranetRepository
{
    /// <summary>کاربر با نقش‌ها و مجوزهای هر نقش (برای ساخت پروفایل و مجوزهای مؤثر).</summary>
    private IQueryable<User> Users => db.Users.Include(u => u.UserRoles).ThenInclude(r => r.Role).ThenInclude(r => r.RolePermissions).ThenInclude(p => p.Permission).AsSplitQuery();
    /// <summary>مجوز با سامانه و نقش‌های دارنده.</summary>
    private IQueryable<Permission> Permissions => db.Permissions.Include(p => p.Application).Include(p => p.RolePermissions).AsSplitQuery();
    /// <summary>کاربر با شناسه (همراه نقش‌ها و مجوزها).</summary>
    public Task<User?> FindUserAsync(long id) => Users.FirstOrDefaultAsync(u => u.UserId == id);
    /// <summary>کاربر با نام کاربری.</summary>
    public Task<User?> FindUserAsync(string username) => Users.FirstOrDefaultAsync(u => u.Username == username);
    public Task<List<User>> FindUsersByNationalIdAsync(string nationalId) => Users.Where(u => u.NationalId == nationalId).Take(2).ToListAsync();
    /// <summary>همه‌ی کاربران با نقش‌هایشان (برای فهرست و آمار).</summary>
    public Task<List<User>> GetUsersAsync() => db.Users.Include(u => u.UserRoles).ThenInclude(r => r.Role).AsSplitQuery().ToListAsync();
    /// <summary>همه‌ی نقش‌ها.</summary>
    public Task<List<Role>> GetRolesAsync() => db.Roles.ToListAsync();
    public Task<Role?> FindRoleAsync(int id) => db.Roles.Include(r => r.UserRoles).ThenInclude(u => u.User)
        .Include(r => r.RolePermissions).ThenInclude(p => p.Permission).ThenInclude(p => p.Application).AsSplitQuery().FirstOrDefaultAsync(r => r.RoleId == id);
    /// <summary>شمار کاربران، مجوزها و اتصال‌ها (دسترسی سامانه و گروه نظارت) هر نقش؛ با چند کوئری گروه‌بندی‌شده نه یک کوئری به‌ازای هر نقش.</summary>
    public async Task<Dictionary<int, RoleUsage>> GetRoleUsageAsync()
    {
        var users = await db.UserRoles.GroupBy(r => r.RoleId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(g => g.Key, g => g.Count);
        var permissions = await db.RolePermissions.GroupBy(r => r.RoleId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(g => g.Key, g => g.Count);
        var access = await db.ApplicationAccessGrants.Where(a => a.RoleId != null).GroupBy(a => a.RoleId!.Value).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(g => g.Key, g => g.Count);
        var supervisors = await db.ApplicationSupervisors.Where(s => s.RoleId != null).GroupBy(s => s.RoleId!.Value).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(g => g.Key, g => g.Count);
        return await db.Roles.Select(r => r.RoleId).ToDictionaryAsync(id => id, id => new RoleUsage(users.GetValueOrDefault(id), permissions.GetValueOrDefault(id),
            access.GetValueOrDefault(id) + supervisors.GetValueOrDefault(id)));
    }
    /// <summary>همه‌ی سامانه‌ها.</summary>
    public Task<List<AppEntity>> GetApplicationsAsync() => db.Applications.ToListAsync();
    /// <summary>فهرست فقط‌خواندنی (بدون ردیابی EF)؛ شمار نقش‌ها و نقش‌های هر مجوز از همان ردیف‌های بارگذاری‌شده می‌آید.</summary>
    public Task<List<Permission>> GetPermissionsAsync() => Permissions.AsNoTracking().ToListAsync();
    public Task<Permission?> FindPermissionAsync(int id) => Permissions.FirstOrDefaultAsync(p => p.PermissionId == id);
    public void AddUser(User user) => db.Users.Add(user);
    public void AddRole(Role role) => db.Roles.Add(role);
    public void RemoveRole(Role role) => db.Roles.Remove(role);
    public void AddPermission(Permission permission) => db.Permissions.Add(permission);
    /// <summary>نقش‌های کاربر را با مجموعه‌ی داده‌شده یکی می‌کند.</summary>
    public void SetUserRoles(User user, IReadOnlyCollection<int> ids)
    {
        db.UserRoles.RemoveRange(user.UserRoles.Where(r => !ids.Contains(r.RoleId)).ToList());
        var existing = user.UserRoles.Select(r => r.RoleId).ToHashSet();
        foreach (var id in ids.Where(id => !existing.Contains(id)))
            user.UserRoles.Add(new UserRole { User = user, RoleId = id, CreatedAt = DateTime.UtcNow });
    }
    /// <summary>نقش‌های دارنده‌ی مجوز را با مجموعه‌ی داده‌شده یکی می‌کند.</summary>
    public void SetPermissionRoles(Permission permission, IReadOnlyCollection<int> ids)
    {
        db.RolePermissions.RemoveRange(permission.RolePermissions.Where(r => !ids.Contains(r.RoleId)).ToList());
        var existing = permission.RolePermissions.Select(r => r.RoleId).ToHashSet();
        foreach (var id in ids.Where(id => !existing.Contains(id)))
            permission.RolePermissions.Add(new RolePermission { Permission = permission, RoleId = id, CreatedAt = DateTime.UtcNow });
    }
    public async Task SaveAsync() => await db.SaveChangesAsync();
}
