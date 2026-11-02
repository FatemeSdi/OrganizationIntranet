using OrganizationIntranet.Application.Abstractions;
using OrganizationIntranet.Contracts;
using OrganizationIntranet.Domain.Entities;

namespace OrganizationIntranet.Application.Services;

/// <summary>منطق کاربران، نقش‌ها و مجوزها: ساخت و ویرایش، پروفایل با مجوزهای مؤثر و قواعد جلوگیری از قفل‌شدن پنل (نقش ADMIN غیرفعال نمی‌شود و همیشه یک مدیر فعال می‌ماند).</summary>
public sealed class IntranetService(IIntranetRepository repository, IPasswordService passwords)
{
    /// <summary>رشته‌ی خالی یا فقط فاصله را null می‌کند.</summary>
    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
    /// <summary>اجباری بودن و حداکثر طول مقدار را بررسی می‌کند.</summary>
    private static void Required(string? value, int length, string name)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > length)
            throw new ArgumentException($"{name} الزامی است و حداکثر {length} کاراکتر دارد");
    }
    /// <summary>حداکثر طول یک مقدار اختیاری را بررسی می‌کند.</summary>
    private static void Limit(string? value, int length)
    {
        if (value?.Length > length) throw new ArgumentException("طول مقدار واردشده بیش از حد مجاز است");
    }
    private static UserDto Map(User u) => new(u.UserId, u.Name, u.LastName, u.Username, u.NationalId, u.MobileNumber, u.Email, u.IsActive, u.CreatedAt, u.LastLogin, string.Join("، ", u.UserRoles.Select(r => r.Role.RoleName)), u.UserRoles.Select(r => r.RoleId).ToList(), u.UnitId);
    private static ProfileDto Profile(User u) => new(u.UserId, u.Username, u.Name, u.LastName, u.NationalId, u.MobileNumber, u.Email, u.CreatedAt, u.LastLogin,
        u.UserRoles.Count > 0 ? string.Join("، ", u.UserRoles.Select(r => r.Role.RoleName)) : "بدون نقش",
        u.UserRoles.Where(r => r.Role.IsActive).Select(r => r.Role.RoleCode).ToList(),
        u.UserRoles.Where(r => r.Role.IsActive).SelectMany(r => r.Role.RolePermissions).Where(r => r.Permission.IsActive).Select(r => r.Permission.PermissionCode).Distinct().ToList());

    /// <summary>پروفایل کاربر با نقش‌ها و مجوزهای مؤثر؛ مجوز فقط از نقش فعال و مجوز فعال می‌آید.</summary>
    public async Task<ProfileDto?> ProfileAsync(long id)
    {
        var user = await repository.FindUserAsync(id);
        return user is null ? null : Profile(user);
    }
    /// <summary>فهرست کاربران به ترتیب نام.</summary>
    public async Task<List<UserDto>> UsersAsync() => (await repository.GetUsersAsync()).OrderBy(u => u.Name).Select(Map).ToList();
    /// <summary>یک کاربر با نقش‌هایش؛ null اگر نباشد.</summary>
    public async Task<UserDto?> UserAsync(long id)
    {
        var user = await repository.FindUserAsync(id);
        return user is null ? null : Map(user);
    }
    /// <summary>آمار داشبورد پنل: شمار کاربران (کل و فعال)، نقش‌ها، سامانه‌ها و پنج کاربر آخر.</summary>
    public async Task<DashboardDto> DashboardAsync()
    {
        var users = await repository.GetUsersAsync();
        return new(users.Count, users.Count(u => u.IsActive), (await repository.GetRolesAsync()).Count, (await repository.GetApplicationsAsync()).Count, users.OrderByDescending(u => u.CreatedAt).Take(5).Select(Map).ToList());
    }
    /// <summary>ساخت (شناسه‌ی خالی) یا ویرایش کاربر همراه نقش‌ها و رمز اختیاری. کاربر جدید رمز می‌خواهد. با هر ذخیره مهر امنیتی عوض می‌شود تا نشست‌های قبلی باطل شوند.</summary>
    public async Task SaveUserAsync(UserEditRequest request)
    {
        Required(request.Name, 100, "نام"); Required(request.LastName, 100, "نام خانوادگی"); Required(request.Username, 100, "نام کاربری");
        Limit(request.NationalId, 10); Limit(request.MobileNumber, 20); Limit(request.Email, 200); Limit(request.NewPassword, 1024);
        var ids = request.SelectedRoleIds.Distinct().ToList();
        await ValidateRoles(ids);
        var unitId = request.UnitId is null or 0 ? null : request.UnitId;
        bool isNew = request.Id is null or 0;
        await EnsureAnotherAdminRemainsAsync(isNew ? null : request.Id, request.IsActive, ids);
        var user = isNew ? new User { CreatedAt = DateTime.UtcNow } : await repository.FindUserAsync(request.Id!.Value) ?? throw new KeyNotFoundException();
        if (isNew) { Required(request.NewPassword, 1024, "رمز عبور"); repository.AddUser(user); }
        user.Name = request.Name; user.LastName = request.LastName; user.Username = request.Username;
        user.NationalId = Optional(request.NationalId); user.MobileNumber = Optional(request.MobileNumber); user.Email = Optional(request.Email); user.IsActive = request.IsActive;
        // Keeping an inactive unit the user already has is allowed; moving a user to a unit needs it to exist and be active.
        if (unitId is not null && unitId != user.UnitId && !await repository.ActiveUnitExistsAsync(unitId.Value))
            throw new ArgumentException("واحد سازمانی انتخاب‌شده وجود ندارد یا غیرفعال است");
        user.UnitId = unitId;
        if (!string.IsNullOrWhiteSpace(request.NewPassword))
        {
            AuthenticationService.ValidateNewPassword(request.NewPassword, request.NewPassword);
            user.PasswordHash = passwords.Hash(user, request.NewPassword);
        }
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        user.AuthenticationVersion = Guid.NewGuid();
        repository.SetUserRoles(user, ids);
        await repository.SaveAsync(); // One SaveChanges transaction also covers a new user's role assignments.
    }
    /// <summary>افزودن یا برداشتن یک نقش از کاربر موجود (ثبت لحظه‌ای)؛ همان قواعد ذخیره‌ی کاربر: نقش معتبر و حداقل یک مدیر فعال باقی بماند. تکرار بی‌اثر است.</summary>
    public async Task SetUserRoleAsync(long userId, UserRoleToggleRequest request)
    {
        var user = await repository.FindUserAsync(userId) ?? throw new KeyNotFoundException();
        var ids = user.UserRoles.Select(r => r.RoleId).ToHashSet();
        if (request.Granted ? !ids.Add(request.RoleId) : !ids.Remove(request.RoleId)) return;
        var list = ids.ToList();
        await ValidateRoles(list);
        await EnsureAnotherAdminRemainsAsync(userId, user.IsActive, list);
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        user.AuthenticationVersion = Guid.NewGuid();
        repository.SetUserRoles(user, list);
        await repository.SaveAsync();
    }
    private static RoleDto Map(Role r, RoleUsage? usage) => new(r.RoleId, r.RoleName, r.RoleCode, r.IsActive, r.CreatedAt, usage?.Users ?? 0, usage?.Permissions ?? 0, usage?.Links ?? 0);
    /// <summary>فهرست نقش‌ها با شمار کاربران، مجوزها و اتصال‌ها و اینکه قابل حذف هستند یا نه.</summary>
    public async Task<List<RoleDto>> RolesAsync()
    {
        var usage = await repository.GetRoleUsageAsync();
        return (await repository.GetRolesAsync()).OrderBy(r => r.RoleName).Select(r => Map(r, usage.GetValueOrDefault(r.RoleId))).ToList();
    }
    /// <summary>جزئیات نقش؛ فهرست کاربران فقط وقتی includeUsers باشد (دارنده‌ی USER_VIEW).</summary>
    public async Task<RoleDetailsDto?> RoleAsync(int id, bool includeUsers)
    {
        var role = await repository.FindRoleAsync(id);
        if (role is null) return null;
        var users = includeUsers
            ? role.UserRoles.Select(r => r.User).OrderByDescending(u => u.IsActive).ThenBy(u => u.LastName).ThenBy(u => u.Name)
                .Select(u => new RoleMemberDto(u.UserId, $"{u.Name} {u.LastName}".Trim(), u.Username, u.IsActive)).ToList()
            : [];
        var permissions = role.RolePermissions.Select(r => r.Permission).OrderBy(p => p.Application.ApplicationName).ThenBy(p => p.PermissionName)
            .Select(p => new RolePermissionItemDto(p.PermissionId, p.PermissionName, p.PermissionCode, p.Application.ApplicationName, p.IsActive)).ToList();
        return new(Map(role, (await repository.GetRoleUsageAsync()).GetValueOrDefault(id)), users, permissions, !includeUsers);
    }
    /// <summary>نام و کد پاک‌شده؛ کد با حروف بزرگ انگلیسی، رقم و _ و یکتا.</summary>
    private async Task<(string Name, string Code)> ValidRoleAsync(RoleRequest request, int? exceptId)
    {
        var name = request.RoleName?.Trim() ?? ""; var code = request.RoleCode?.Trim().ToUpperInvariant() ?? "";
        Required(name, 100, "نام نقش"); Required(code, 50, "کد نقش");
        if (!System.Text.RegularExpressions.Regex.IsMatch(code, "^[A-Z][A-Z0-9_]*$"))
            throw new ArgumentException("کد نقش فقط حروف انگلیسی، رقم و _ دارد و با حرف شروع می‌شود (مثل HR_EXPERT)");
        if ((await repository.GetRolesAsync()).Any(r => r.RoleId != exceptId && string.Equals(r.RoleCode, code, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException($"نقشی با کد {code} از قبل وجود دارد");
        return (name, code);
    }
    /// <summary>ساخت نقش تازه با کد یکتا.</summary>
    public async Task CreateRoleAsync(RoleRequest request)
    {
        var (name, code) = await ValidRoleAsync(request, null);
        repository.AddRole(new Role { RoleName = name, RoleCode = code, IsActive = true, CreatedAt = DateTime.UtcNow });
        await repository.SaveAsync();
    }
    /// <summary>ویرایش نام و کد نقش؛ کد نقش‌های سیستمی ADMIN و EXPERT عوض نمی‌شود چون برنامه آن‌ها را با کد پیدا می‌کند.</summary>
    public async Task UpdateRoleAsync(int id, RoleRequest request)
    {
        var role = (await repository.GetRolesAsync()).FirstOrDefault(r => r.RoleId == id) ?? throw new KeyNotFoundException();
        var (name, code) = await ValidRoleAsync(request, id);
        if (code != role.RoleCode && AdminPermissions.IsSystemRole(role.RoleCode))
            throw new ArgumentException($"کد نقش سیستمی {role.RoleCode} در برنامه استفاده می‌شود و تغییر نمی‌کند؛ فقط نام آن قابل ویرایش است");
        role.RoleName = name; role.RoleCode = code;
        await repository.SaveAsync();
    }
    /// <summary>فقط نقش بدون کاربر، مجوز و اتصال حذف می‌شود؛ قید FK بانک هم در همزمانی جلوی حذف نقش استفاده‌شده را می‌گیرد.</summary>
    public async Task DeleteRoleAsync(int id)
    {
        var role = (await repository.GetRolesAsync()).FirstOrDefault(r => r.RoleId == id) ?? throw new KeyNotFoundException();
        if (AdminPermissions.IsSystemRole(role.RoleCode)) throw new ArgumentException($"نقش سیستمی {role.RoleCode} حذف نمی‌شود");
        var usage = (await repository.GetRoleUsageAsync()).GetValueOrDefault(id) ?? new RoleUsage(0, 0, 0);
        var uses = new List<string>();
        if (usage.Users > 0) uses.Add($"{usage.Users} کاربر");
        if (usage.Permissions > 0) uses.Add($"{usage.Permissions} مجوز");
        if (usage.Links > 0) uses.Add($"{usage.Links} دسترسی سامانه یا گروه نظارت");
        if (uses.Count > 0) throw new ArgumentException($"این نقش {string.Join("، ", uses)} دارد و حذف نمی‌شود؛ ابتدا آن‌ها را جدا کنید یا نقش را غیرفعال کنید");
        repository.RemoveRole(role);
        await repository.SaveAsync();
    }
    /// <summary>فعال/غیرفعال کردن نقش؛ ADMIN غیرفعال نمی‌شود و نقش غیرفعال هیچ مجوز یا دسترسی نمی‌دهد.</summary>
    public async Task ToggleRoleAsync(int id)
    {
        var entity = (await repository.GetRolesAsync()).FirstOrDefault(r => r.RoleId == id) ?? throw new KeyNotFoundException();
        if (entity.IsActive && entity.RoleCode == AdminPermissions.AdminRoleCode)
            throw new ArgumentException("نقش مدیر (ADMIN) غیرفعال نمی‌شود؛ بدون آن هیچ‌کس به پنل مدیریت دسترسی ندارد");
        entity.IsActive = !entity.IsActive; await repository.SaveAsync();
    }
    private static PermissionDto Map(Permission p) => new(p.PermissionId, p.PermissionName, p.PermissionCode, p.Description, p.Application.ApplicationName, p.IsActive, p.RolePermissions.Count, p.RolePermissions.Select(r => r.RoleId).ToList());
    /// <summary>فهرست مجوزها به ترتیب سامانه و نام، با شمار نقش‌های دارنده.</summary>
    public async Task<List<PermissionDto>> PermissionsAsync() => (await repository.GetPermissionsAsync()).OrderBy(p => p.Application.ApplicationName).ThenBy(p => p.PermissionName).Select(Map).ToList();
    /// <summary>یک مجوز با شناسه‌ی نقش‌های دارنده‌اش؛ null اگر نباشد.</summary>
    public async Task<PermissionDto?> PermissionAsync(int id)
    {
        var p = await repository.FindPermissionAsync(id); return p is null ? null : Map(p);
    }
    /// <summary>ساخت مجوز تازه برای یک سامانه‌ی فعال؛ کد با حروف بزرگ ذخیره می‌شود.</summary>
    public async Task CreatePermissionAsync(PermissionRequest request)
    {
        Required(request.PermissionName, 150, "نام مجوز"); Required(request.PermissionCode, 100, "کد مجوز"); Limit(request.Description, 500);
        if (!(await repository.GetApplicationsAsync()).Any(a => a.ApplicationId == request.ApplicationId && a.IsActive)) throw new ArgumentException("سامانه انتخاب‌شده معتبر نیست");
        repository.AddPermission(new Permission { PermissionName = request.PermissionName, PermissionCode = request.PermissionCode.ToUpperInvariant(), ApplicationId = request.ApplicationId, Description = Optional(request.Description), IsActive = true, CreatedAt = DateTime.UtcNow });
        await repository.SaveAsync();
    }
    /// <summary>فعال/غیرفعال کردن مجوز؛ مجوزهای سیستمی پنل غیرفعال نمی‌شوند.</summary>
    public async Task TogglePermissionAsync(int id)
    {
        var p = await repository.FindPermissionAsync(id) ?? throw new KeyNotFoundException();
        if (p.IsActive && AdminPermissions.IsSystem(p.PermissionCode)) throw new ArgumentException("مجوزهای سیستمی پنل مدیریت غیرفعال نمی‌شوند");
        p.IsActive = !p.IsActive; await repository.SaveAsync();
    }
    /// <summary>جایگزینی مجموعه‌ی نقش‌های دارنده‌ی مجوز؛ مجوز سیستمی از نقش ADMIN گرفته نمی‌شود.</summary>
    public async Task AssignPermissionAsync(int id, RolesRequest request)
    {
        var p = await repository.FindPermissionAsync(id) ?? throw new KeyNotFoundException();
        var ids = request.SelectedRoleIds.Distinct().ToList(); await ValidateRoles(ids);
        if (AdminPermissions.IsSystem(p.PermissionCode) && (await repository.GetRolesAsync()).FirstOrDefault(r => r.RoleCode == AdminPermissions.AdminRoleCode) is { } admin && !ids.Contains(admin.RoleId))
            throw new ArgumentException("این مجوز سیستمی است و از نقش مدیر (ADMIN) گرفته نمی‌شود");
        repository.SetPermissionRoles(p, ids); await repository.SaveAsync();
    }
    /// <summary>پس از ذخیره‌ی کاربر باید دست‌کم یک کاربر فعال با نقش فعال ADMIN بماند تا کسی از پنل مدیریت قفل نشود.</summary>
    private async Task EnsureAnotherAdminRemainsAsync(long? userId, bool willBeActive, List<int> roleIds)
    {
        var admin = (await repository.GetRolesAsync()).FirstOrDefault(r => r.RoleCode == AdminPermissions.AdminRoleCode && r.IsActive);
        if (admin is null || userId is null || (willBeActive && roleIds.Contains(admin.RoleId))) return;
        bool IsAdmin(User u) => u.IsActive && u.UserRoles.Any(r => r.RoleId == admin.RoleId);
        var users = await repository.GetUsersAsync();
        if (users.Any(u => u.UserId == userId && IsAdmin(u)) && !users.Any(u => u.UserId != userId && IsAdmin(u)))
            throw new ArgumentException("دست‌کم یک کاربر فعال باید نقش مدیر (ADMIN) داشته باشد؛ ابتدا نقش مدیر را به کاربر دیگری بدهید");
    }
    /// <summary>همه‌ی شناسه‌های نقش باید موجود و فعال باشند.</summary>
    private async Task ValidateRoles(List<int> ids)
    {
        var roles = await repository.GetRolesAsync();
        if (ids.Any(id => !roles.Any(r => r.RoleId == id && r.IsActive))) throw new ArgumentException("نقش انتخاب‌شده معتبر یا فعال نیست");
    }
}
