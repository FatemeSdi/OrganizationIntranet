namespace OrganizationIntranet.Contracts;

/// <summary>
/// مجوزهای پنل مدیریت اینترانت. هر صفحه/endpoint مدیریت یکی از این‌ها را لازم دارد؛ نقش ADMIN همه را دارد (migration 008)
/// و این مجوزها از نقش ADMIN گرفتنی یا غیرفعال‌شدنی نیستند تا مدیر از پنل قفل نشود.
/// </summary>
public static class AdminPermissions
{
    public const string ApplicationCode = "INTERANET";
    public const string AdminRoleCode = "ADMIN";
    /// <summary>نقشی که import کاتالوگ سامانه‌ها (OrganizationCatalogImportService) با کد پیدا می‌کند.</summary>
    public const string CatalogImportRoleCode = "EXPERT";

    public const string IntranetAdmin = "INTRANET_ADMIN";
    public const string UserView = "USER_VIEW", UserEdit = "USER_EDIT";
    public const string RoleView = "ROLE_VIEW", RoleEdit = "ROLE_EDIT";
    public const string PermissionView = "PERMISSION_VIEW", PermissionEdit = "PERMISSION_EDIT";
    public const string ApplicationView = "APPLICATION_VIEW", ApplicationEdit = "APPLICATION_EDIT";
    public const string AccessRequestReview = "ACCESS_REQUEST_REVIEW";
    public const string PublicationEdit = "PUBLICATION_EDIT";
    public const string AuthenticationSettingsEdit = "AUTH_SETTINGS_EDIT";

    /// <summary>نام Policy برای [Authorize(Policy = ...)]؛ PermissionPolicyProvider آن را می‌سازد.</summary>
    public const string PolicyPrefix = "Permission:";
    public const string IntranetAdminPolicy = PolicyPrefix + IntranetAdmin;
    public const string UserViewPolicy = PolicyPrefix + UserView, UserEditPolicy = PolicyPrefix + UserEdit;
    public const string RoleViewPolicy = PolicyPrefix + RoleView, RoleEditPolicy = PolicyPrefix + RoleEdit;
    public const string PermissionViewPolicy = PolicyPrefix + PermissionView, PermissionEditPolicy = PolicyPrefix + PermissionEdit;
    public const string ApplicationViewPolicy = PolicyPrefix + ApplicationView, ApplicationEditPolicy = PolicyPrefix + ApplicationEdit;
    public const string AccessRequestReviewPolicy = PolicyPrefix + AccessRequestReview;
    public const string PublicationEditPolicy = PolicyPrefix + PublicationEdit;
    public const string AuthenticationSettingsEditPolicy = PolicyPrefix + AuthenticationSettingsEdit;

    /// <summary>همه‌ی مجوزهای سیستمی پنل با نام فارسی؛ همان فهرستی که migration 008 ثبت می‌کند.</summary>
    public static readonly IReadOnlyList<(string Code, string Name)> All =
    [
        (IntranetAdmin, "ورود به پنل مدیریت اینترانت"),
        (UserView, "مشاهده‌ی کاربران"), (UserEdit, "ویرایش کاربران"),
        (RoleView, "مشاهده‌ی نقش‌ها"), (RoleEdit, "ویرایش نقش‌ها"),
        (PermissionView, "مشاهده‌ی مجوزها"), (PermissionEdit, "ویرایش و اختصاص مجوزها"),
        (ApplicationView, "مشاهده‌ی سامانه‌ها"), (ApplicationEdit, "ویرایش سامانه‌ها و دسترسی آن‌ها"),
        (AccessRequestReview, "بررسی درخواست‌های دسترسی"),
        (PublicationEdit, "مدیریت اطلاع‌رسانی سازمانی"),
        (AuthenticationSettingsEdit, "تنظیمات ورود و امنیت"),
    ];

    public static bool IsSystem(string? code) => code is not null && All.Any(p => p.Code == code);

    /// <summary>نقش‌هایی که کد برنامه با کدشان پیدا می‌کند: کدشان عوض نمی‌شود و حذف نمی‌شوند.</summary>
    public static bool IsSystemRole(string? code) => code is AdminRoleCode or CatalogImportRoleCode;
}
