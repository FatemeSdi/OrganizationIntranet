using OrganizationIntranet.Contracts;
using OrganizationIntranet.Domain.Entities;

namespace OrganizationIntranet.Api.Data;

/// <summary>کوئری‌های مشترک نظارت: چه کسانی هشدار درخواست می‌گیرند و هر کاربر ناظر کدام سامانه‌هاست.</summary>
internal static class SupervisionQueries
{
    /// <summary>بررسی‌کنندگان درخواست دسترسی: کاربران فعال که مجوز فعال ACCESS_REQUEST_REVIEW را از یک نقش فعال دارند (نقش ADMIN آن را دارد). آن‌ها ناظر همه‌ی سامانه‌ها حساب می‌شوند.</summary>
    private static IQueryable<User> Reviewers(AppDbContext db) => db.Users.Where(u => u.IsActive && u.UserRoles.Any(ur => ur.Role.IsActive
        && ur.Role.RolePermissions.Any(rp => rp.Permission.IsActive && rp.Permission.PermissionCode == AdminPermissions.AccessRequestReview)));

    /// <summary>گیرندگان هشدار یک درخواست: همه‌ی بررسی‌کنندگان به‌علاوه‌ی اعضای (مستقیم یا از راه نقش) گروه‌های نظارت فعال همان سامانه.</summary>
    internal static IQueryable<User> Recipients(AppDbContext db, int applicationId) => db.Users.Where(u => u.IsActive &&
        (Reviewers(db).Any(r => r.UserId == u.UserId)
         || db.ApplicationSupervisionGroups.Any(g => g.ApplicationId == applicationId && g.IsActive && g.Application.IsActive
            && g.Members.Any(m => m.UserId == u.UserId || (m.RoleId != null && m.Role!.IsActive && m.Role.UserRoles.Any(ur => ur.UserId == u.UserId))))));
    /// <summary>شناسه‌ی سامانه‌هایی که کاربر ناظرشان است: بررسی‌کننده = همه‌ی سامانه‌ها؛ در غیر این صورت فقط سامانه‌های فعال با گروه نظارت فعالی که او عضو آن است.</summary>
    internal static IQueryable<int> Applications(AppDbContext db, long userId) => db.Applications.Where(a =>
        Reviewers(db).Any(r => r.UserId == userId)
        || (a.IsActive && db.Users.Any(u => u.UserId == userId && u.IsActive) && a.SupervisionGroups.Any(g => g.IsActive
            && g.Members.Any(m => m.UserId == userId || (m.RoleId != null && m.Role!.IsActive && m.Role.UserRoles.Any(ur => ur.UserId == userId))))))
        .Select(a => a.ApplicationId);
    /// <summary>هشدارهای درون‌برنامه‌ای کاربر، فقط برای سامانه‌هایی که هنوز ناظرشان است.</summary>
    internal static IQueryable<AccessRequestAlert> Alerts(AppDbContext db, long userId)
    {
        var apps = Applications(db, userId);
        return db.AccessRequestAlerts.Where(a => a.UserId == userId && a.InApp && apps.Contains(a.Request.ApplicationId));
    }
}
