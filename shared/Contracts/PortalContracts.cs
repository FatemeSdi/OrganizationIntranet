namespace OrganizationIntranet.Contracts;

public record LoginRequest(string Username, string Password, string Method = "password");
public record RefreshRequest(string RefreshToken);
public record ResetPasswordRequest(string Username, string NationalId, string MobileNumber, string NewPassword, string ConfirmNewPassword);
public record ChangePasswordRequest(string CurrentPassword, string NewPassword, string ConfirmNewPassword);
public record RoleRequest(string RoleName, string RoleCode);
public record ApplicationRequest(string ApplicationName, string ApplicationCode, string? BaseUrl, string? Description, int DisplayOrder, string? Icon = "apps", bool RequiresLogin = true, bool IsInternetAccessible = false, bool IsPublic = false, string AccessRequestNotificationChannels = "InApp");
public record PermissionRequest(string PermissionName, string PermissionCode, int ApplicationId, string? Description);
public record RolesRequest(List<int> SelectedRoleIds);
public record OperationResult(bool Success, string? Message = null);
/// <summary>LinkCount: دسترسی مستقیم سامانه‌ها و عضویت در گروه‌های نظارت که به این نقش داده شده است.</summary>
public record RoleDto(int RoleId, string RoleName, string RoleCode, bool IsActive, DateTime CreatedAt, int UserCount = 0, int PermissionCount = 0, int LinkCount = 0)
{
    /// <summary>نقش بدون کاربر، مجوز یا اتصال را می‌توان حذف کرد؛ نقش‌های سیستمی هرگز.</summary>
    public bool CanDelete => !AdminPermissions.IsSystemRole(RoleCode) && UserCount == 0 && PermissionCount == 0 && LinkCount == 0;
}
public record RoleMemberDto(long UserId, string DisplayName, string Username, bool IsActive);
public record RolePermissionItemDto(int PermissionId, string PermissionName, string PermissionCode, string ApplicationName, bool IsActive);
/// <summary>Users فقط برای دارنده‌ی USER_VIEW پر می‌شود؛ UsersHidden یعنی فهرست به دلیل نبود مجوز خالی است.</summary>
public record RoleDetailsDto(RoleDto Role, List<RoleMemberDto> Users, List<RolePermissionItemDto> Permissions, bool UsersHidden = false);
public record ApplicationDto(int ApplicationId, string ApplicationName, string ApplicationCode, string? BaseUrl, string? Description, int DisplayOrder, bool IsActive, string? Icon = "apps", bool RequiresLogin = true, bool IsInternetAccessible = false, bool IsPublic = false, string AccessRequestNotificationChannels = "InApp");
public record PermissionDto(int PermissionId, string PermissionName, string PermissionCode, string? Description, string ApplicationName, bool IsActive, int RoleCount, List<int> SelectedRoleIds);
public record UserDto(long UserId, string Name, string LastName, string Username, string? NationalId, string? MobileNumber, string? Email, bool IsActive, DateTime CreatedAt, DateTime? LastLogin, string Roles, List<int> SelectedRoleIds);
public record ProfileDto(long UserId, string Username, string Name, string LastName, string? NationalId, string? MobileNumber, string? Email, DateTime CreatedAt, DateTime? LastLogin, string RoleNames, List<string> RoleCodes, List<string> PermissionCodes)
{
    public string DisplayName => $"{Name} {LastName}".Trim();
    public string Initials => (Name.Length > 0 ? Name[..1] : "") + (LastName.Length > 0 ? LastName[..1] : "");
}
public record DashboardDto(int TotalUsers, int ActiveUsers, int TotalRoles, int TotalApplications, List<UserDto> RecentUsers);
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
}
