using OrganizationIntranet.Domain.Entities;
using AppEntity = OrganizationIntranet.Domain.Entities.Application;

namespace OrganizationIntranet.Application.Abstractions;

// Returned entities are tracked for the lifetime of one API request.
public interface IIntranetRepository
{
    Task<User?> FindUserAsync(long id);
    Task<User?> FindUserAsync(string username);
    Task<List<User>> GetUsersAsync();
    Task<List<Role>> GetRolesAsync();
    Task<List<AppEntity>> GetApplicationsAsync();
    Task<List<Permission>> GetPermissionsAsync();
    Task<Permission?> FindPermissionAsync(int id);
    void AddUser(User user);
    void AddRole(Role role);
    void AddApplication(AppEntity application);
    void AddPermission(Permission permission);
    void SetUserRoles(User user, IReadOnlyCollection<int> ids);
    void SetPermissionRoles(Permission permission, IReadOnlyCollection<int> ids);
    Task SaveAsync();
}

public interface IPasswordService
{
    string Hash(User user, string password);
    bool Verify(User user, string password);
}
