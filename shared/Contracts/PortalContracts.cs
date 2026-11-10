namespace OrganizationIntranet.Contracts;

/// <summary>درخواست ورود: نام کاربری، رمز و روش (password یا ldap).</summary>
public record LoginRequest(string Username, string Password, string Method = "password");
/// <summary>تمدید توکن با Refresh Token.</summary>
public record RefreshRequest(string RefreshToken);
/// <summary>قالب قدیمی بازیابی رمز با کد ملی و موبایل؛ دیگر پذیرفته نمی‌شود (بازیابی فقط با کد پیامکی است) و فقط آزمون رد آن را می‌سنجد.</summary>
public record ResetPasswordRequest(string Username, string NationalId, string MobileNumber, string NewPassword, string ConfirmNewPassword);
/// <summary>تغییر رمز: رمز فعلی، رمز جدید و تأیید آن.</summary>
public record ChangePasswordRequest(string CurrentPassword, string NewPassword, string ConfirmNewPassword);
/// <summary>ساخت یا ویرایش نقش: نام و کد (حروف بزرگ انگلیسی، رقم و _).</summary>
public record RoleRequest(string RoleName, string RoleCode);
/// <summary>مشخصات سامانه برای ساخت یا ویرایش. BaseUrl الزامی است؛ DisplayOrder خالی یعنی آخرین ردیف.</summary>
public record ApplicationRequest(string ApplicationName, string ApplicationCode, string? BaseUrl, string? Description, int? DisplayOrder, string? Icon = "apps", bool RequiresLogin = true, bool IsInternetAccessible = false, bool IsPublic = false, string AccessRequestNotificationChannels = "InApp", bool IsUnitRestricted = false);
/// <summary>افزودن یا برداشتن یک کاربر (UserId) یا یک نقش (RoleId) از فهرست دسترسی؛ فقط یکی از دو مقدار پر است. برای ثبت لحظه‌ای بدون دکمه‌ی ذخیره.</summary>
public record AccessToggleRequest(long? UserId, int? RoleId, bool Granted);
/// <summary>افزودن یا برداشتن یک نقش از کاربر؛ برای ثبت لحظه‌ای.</summary>
public record UserRoleToggleRequest(int RoleId, bool Granted);
/// <summary>ساخت مجوز تازه برای یک سامانه.</summary>
public record PermissionRequest(string PermissionName, string PermissionCode, int ApplicationId, string? Description);
/// <summary>مجموعه‌ی کامل نقش‌های دارنده‌ی یک مجوز.</summary>
public record RolesRequest(List<int> SelectedRoleIds);
/// <summary>نتیجه‌ی یک عملیات: موفق بودن و پیام فارسی برای کاربر (در خطای اعتبارسنجی، پیام علت).</summary>
public record OperationResult(bool Success, string? Message = null);
/// <summary>LinkCount: دسترسی مستقیم سامانه‌ها و عضویت در گروه‌های نظارت که به این نقش داده شده است.</summary>
public record RoleDto(int RoleId, string RoleName, string RoleCode, bool IsActive, DateTime CreatedAt, int UserCount = 0, int PermissionCount = 0, int LinkCount = 0)
{
    /// <summary>نقش بدون کاربر، مجوز یا اتصال را می‌توان حذف کرد؛ نقش‌های سیستمی هرگز.</summary>
    public bool CanDelete => !AdminPermissions.IsSystemRole(RoleCode) && UserCount == 0 && PermissionCount == 0 && LinkCount == 0;
}
/// <summary>یک عضو نقش در صفحه‌ی جزئیات نقش.</summary>
public record RoleMemberDto(long UserId, string DisplayName, string Username, bool IsActive);
/// <summary>یک مجوز نقش در صفحه‌ی جزئیات نقش.</summary>
public record RolePermissionItemDto(int PermissionId, string PermissionName, string PermissionCode, string ApplicationName, bool IsActive);
/// <summary>Users فقط برای دارنده‌ی USER_VIEW پر می‌شود؛ UsersHidden یعنی فهرست به دلیل نبود مجوز خالی است.</summary>
public record RoleDetailsDto(RoleDto Role, List<RoleMemberDto> Users, List<RolePermissionItemDto> Permissions, bool UsersHidden = false);
/// <summary>سامانه در فهرست‌های پنل و فرم‌ها (بدون دسترسی‌ها و سرویس‌ها).</summary>
public record ApplicationDto(int ApplicationId, string ApplicationName, string ApplicationCode, string? BaseUrl, string? Description, int DisplayOrder, bool IsActive, string? Icon = "apps", bool RequiresLogin = true, bool IsInternetAccessible = false, bool IsPublic = false, string AccessRequestNotificationChannels = "InApp", bool IsUnitRestricted = false, long ImageVersion = 0)
{
    /// <summary>آیا سامانه تصویر کارت دارد؟</summary>
    public bool HasImage => ImageVersion != 0;
}
/// <summary>مجوز برای فهرست پنل: سامانه، شمار نقش‌ها و شناسه‌ی نقش‌های دارنده.</summary>
public record PermissionDto(int PermissionId, string PermissionName, string PermissionCode, string? Description, string ApplicationName, bool IsActive, int RoleCount, List<int> SelectedRoleIds);
/// <summary>کاربر برای فهرست و فرم ویرایش پنل (بدون رمز و رازها).</summary>
public record UserDto(long UserId, string Name, string LastName, string Username, string? NationalId, string? MobileNumber, string? Email, bool IsActive, DateTime CreatedAt, DateTime? LastLogin, string Roles, List<int> SelectedRoleIds, int? UnitId = null);
/// <summary>پروفایل کاربر واردشده با نقش‌ها و مجوزهای مؤثر؛ UI مجوزها را از همین در Claim کوکی می‌گذارد و هر ۵ دقیقه تازه می‌کند.</summary>
public record ProfileDto(long UserId, string Username, string Name, string LastName, string? NationalId, string? MobileNumber, string? Email, DateTime CreatedAt, DateTime? LastLogin, string RoleNames, List<string> RoleCodes, List<string> PermissionCodes)
{
    public string DisplayName => $"{Name} {LastName}".Trim();
    public string Initials => (Name.Length > 0 ? Name[..1] : "") + (LastName.Length > 0 ? LastName[..1] : "");
}
/// <summary>آمار داشبورد پنل مدیریت.</summary>
public record DashboardDto(int TotalUsers, int ActiveUsers, int TotalRoles, int TotalApplications, List<UserDto> RecentUsers);
/// <summary>ساخت یا ویرایش کاربر در پنل؛ Id خالی یعنی کاربر جدید که NewPassword دارد.</summary>
public class UserEditRequest
{
    public long? Id { get; set; }
    public string Name { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Username { get; set; } = "";
    public string? NationalId { get; set; }
    public string? MobileNumber { get; set; }
    public string? Email { get; set; }
    public bool IsActive { get; set; } = true;
    public string? NewPassword { get; set; }
    public List<int> SelectedRoleIds { get; set; } = new();
    /// <summary>واحد کاربر در چارت سازمانی؛ خالی یعنی بدون واحد.</summary>
    public int? UnitId { get; set; }
}
