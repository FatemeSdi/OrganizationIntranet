using Microsoft.EntityFrameworkCore;
using OrganizationIntranet.Application.Abstractions;
using OrganizationIntranet.Domain.Entities;
using AppEntity = OrganizationIntranet.Domain.Entities.Application;

namespace OrganizationIntranet.Api.Data;

public sealed class IntranetRepository(AppDbContext db) : IIntranetRepository
{
    private IQueryable<User> Users => db.Users.Include(u => u.UserRoles).ThenInclude(r => r.Role).ThenInclude(r => r.RolePermissions).ThenInclude(p => p.Permission).AsSplitQuery();
    private IQueryable<Permission> Permissions => db.Permissions.Include(p => p.Application).Include(p => p.RolePermissions).AsSplitQuery();
    public Task<User?> FindUserAsync(long id) => Users.FirstOrDefaultAsync(u => u.UserId == id);
    public Task<User?> FindUserAsync(string username) => Users.FirstOrDefaultAsync(u => u.Username == username);
    public Task<List<User>> GetUsersAsync() => db.Users.Include(u => u.UserRoles).ThenInclude(r => r.Role).AsSplitQuery().ToListAsync();
    public Task<List<Role>> GetRolesAsync() => db.Roles.ToListAsync();
    public Task<List<AppEntity>> GetApplicationsAsync() => db.Applications.ToListAsync();
    public Task<List<Permission>> GetPermissionsAsync() => Permissions.ToListAsync();
    public Task<Permission?> FindPermissionAsync(int id) => Permissions.FirstOrDefaultAsync(p => p.PermissionId == id);
    public void AddUser(User user) => db.Users.Add(user);
    public void AddRole(Role role) => db.Roles.Add(role);
    public void AddApplication(AppEntity application) => db.Applications.Add(application);
    public void AddPermission(Permission permission) => db.Permissions.Add(permission);
    public void SetUserRoles(User user, IReadOnlyCollection<int> ids)
    {
        db.UserRoles.RemoveRange(user.UserRoles.Where(r => !ids.Contains(r.RoleId)).ToList());
        var existing = user.UserRoles.Select(r => r.RoleId).ToHashSet();
        foreach (var id in ids.Where(id => !existing.Contains(id)))
            user.UserRoles.Add(new UserRole { User = user, RoleId = id, CreatedAt = DateTime.UtcNow });
    }
    public void SetPermissionRoles(Permission permission, IReadOnlyCollection<int> ids)
    {
        db.RolePermissions.RemoveRange(permission.RolePermissions.Where(r => !ids.Contains(r.RoleId)).ToList());
        var existing = permission.RolePermissions.Select(r => r.RoleId).ToHashSet();
        foreach (var id in ids.Where(id => !existing.Contains(id)))
            permission.RolePermissions.Add(new RolePermission { Permission = permission, RoleId = id, CreatedAt = DateTime.UtcNow });
    }
    public async Task SaveAsync() => await db.SaveChangesAsync();
}
