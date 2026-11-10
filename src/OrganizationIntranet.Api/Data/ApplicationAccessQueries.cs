using AppEntity = OrganizationIntranet.Domain.Entities.Application;

namespace OrganizationIntranet.Api.Data;

/// <summary>کوئری مشترک «سامانه‌های در اختیار کاربر»: دید پورتال، ورود یکپارچه، اعلان‌ها و درخواست دسترسی همه از همین یک تعریف می‌آیند تا هرگز با هم ناسازگار نشوند.</summary>
internal static class ApplicationAccessQueries
{
    /// <summary>
    /// سامانه‌های فعالی که کاربر (فعال) همین الان به آن‌ها دسترسی دارد: عمومی بودن، یا دسترسی مستقیم، یا دسترسی از راه نقش فعالِ او،
    /// یا (برای سامانه‌ی «محدود به واحد») بودن در یکی از واحدهای انتخاب‌شده یا زیرواحدهای آن‌ها. زیرواحد با پیشوند مسیر کامل
    /// (<c>مسیر-واحد + "/"</c>) شناخته می‌شود و واحد غیرفعال، دسترسی نمی‌دهد و نمی‌گیرد. زنده از بانک خوانده می‌شود، نه از Claimهای توکن.
    /// </summary>
    internal static IQueryable<AppEntity> Available(AppDbContext db, long userId) => db.Applications.Where(a => a.IsActive
        && db.Users.Any(u => u.UserId == userId && u.IsActive)
        && (a.IsPublic || a.AccessGrants.Any(g => g.UserId == userId
            || (g.RoleId != null && g.Role!.IsActive && g.Role.UserRoles.Any(ur => ur.UserId == userId)))
            || (a.IsUnitRestricted && a.UnitAccess.Any(ua => ua.Unit.IsActive && db.Users.Any(u => u.UserId == userId && u.Unit != null && u.Unit.IsActive
                && (u.Unit.FullPath == ua.Unit.FullPath || u.Unit.FullPath.StartsWith(ua.Unit.FullPath + "/")))))));
}
