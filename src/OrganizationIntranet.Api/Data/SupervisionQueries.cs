using OrganizationIntranet.Contracts;
using OrganizationIntranet.Domain.Entities;

namespace OrganizationIntranet.Api.Data;

internal static class SupervisionQueries
{
    // بررسی‌کنندگان درخواست دسترسی کسانی‌اند که مجوز فعال ACCESS_REQUEST_REVIEW را از یک نقش فعال دارند (نقش ADMIN آن را دارد).
    private static IQueryable<User> Reviewers(AppDbContext db) => db.Users.Where(u => u.IsActive && u.UserRoles.Any(ur => ur.Role.IsActive
        && ur.Role.RolePermissions.Any(rp => rp.Permission.IsActive && rp.Permission.PermissionCode == AdminPermissions.AccessRequestReview)));

    internal static IQueryable<User> Recipients(AppDbContext db, int applicationId) => db.Users.Where(u => u.IsActive &&
        (Reviewers(db).Any(r => r.UserId == u.UserId)
         || db.ApplicationSupervisionGroups.Any(g => g.ApplicationId == applicationId && g.IsActive && g.Application.IsActive
            && g.Members.Any(m => m.UserId == u.UserId || (m.RoleId != null && m.Role!.IsActive && m.Role.UserRoles.Any(ur => ur.UserId == u.UserId))))));
    internal static IQueryable<int> Applications(AppDbContext db, long userId) => db.Applications.Where(a =>
        Reviewers(db).Any(r => r.UserId == userId)
        || (a.IsActive && db.Users.Any(u => u.UserId == userId && u.IsActive) && a.SupervisionGroups.Any(g => g.IsActive
            && g.Members.Any(m => m.UserId == userId || (m.RoleId != null && m.Role!.IsActive && m.Role.UserRoles.Any(ur => ur.UserId == userId))))))
        .Select(a => a.ApplicationId);
}
