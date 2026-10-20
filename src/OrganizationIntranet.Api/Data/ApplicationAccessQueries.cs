using AppEntity = OrganizationIntranet.Domain.Entities.Application;

namespace OrganizationIntranet.Api.Data;

internal static class ApplicationAccessQueries
{
    internal static IQueryable<AppEntity> Available(AppDbContext db, long userId) => db.Applications.Where(a => a.IsActive
        && db.Users.Any(u => u.UserId == userId && u.IsActive)
        && (a.IsPublic || a.AccessGrants.Any(g => g.UserId == userId
            || (g.RoleId != null && g.Role!.IsActive && g.Role.UserRoles.Any(ur => ur.UserId == userId)))));
}
