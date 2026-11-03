using OrganizationIntranet.Application.Abstractions;
using OrganizationIntranet.Contracts;
using OrganizationIntranet.Domain.Entities;

namespace OrganizationIntranet.Application.Services;

/// <summary>
/// ساخت اولین مدیر روی نصب تازه (یا بانکی که هیچ مدیر فعالی ندارد). فقط وقتی کار می‌کند که <b>هیچ کاربر فعالی نقش فعال ADMIN را نداشته باشد</b>؛
/// پس نمی‌توان با آن مدیر اضافه کرد یا حساب موجود را ارتقا داد. رمز با همان قاعده‌ی تغییر رمز (۱۰ تا ۱۲۸ نویسه) بررسی و فقط به‌صورت هش ذخیره می‌شود.
/// </summary>
public sealed class FirstAdminService(IIntranetRepository repository, IPasswordService passwords)
{
    /// <summary>آیا کاربر فعالی با نقش فعال ADMIN وجود دارد؟</summary>
    public async Task<bool> AdminExistsAsync() => (await repository.GetUsersAsync())
        .Any(u => u.IsActive && u.UserRoles.Any(r => r.Role.IsActive && r.Role.RoleCode == AdminPermissions.AdminRoleCode));

    /// <summary>کاربر فعال تازه با نقش ADMIN می‌سازد و شناسه‌اش را برمی‌گرداند؛ در صورت وجود مدیر فعال، نبود نقش ADMIN یا تکراری بودن نام کاربری خطا می‌دهد.</summary>
    public async Task<long> CreateAsync(string? username, string? name, string? lastName, string? password)
    {
        username = username?.Trim(); name = name?.Trim(); lastName = lastName?.Trim();
        if (string.IsNullOrEmpty(username) || username.Length > 100 || username.Any(c => char.IsWhiteSpace(c) || char.IsControl(c)))
            throw new ArgumentException("نام کاربری الزامی است، حداکثر ۱۰۰ نویسه و بدون فاصله");
        if (string.IsNullOrEmpty(name) || name.Length > 100 || string.IsNullOrEmpty(lastName) || lastName.Length > 100)
            throw new ArgumentException("نام و نام خانوادگی الزامی است و هرکدام حداکثر ۱۰۰ نویسه دارد");
        AuthenticationService.ValidateNewPassword(password ?? "", password ?? "");
        if (await AdminExistsAsync()) throw new InvalidOperationException("از قبل یک مدیر فعال وجود دارد؛ این ابزار فقط برای نصب بدون مدیر است. مدیر جدید را از پنل بسازید.");
        var role = (await repository.GetRolesAsync()).FirstOrDefault(r => r.IsActive && r.RoleCode == AdminPermissions.AdminRoleCode)
            ?? throw new InvalidOperationException("نقش فعال ADMIN در بانک نیست؛ ابتدا نصب بانک (خط پایه و migrationها) را کامل کنید.");
        if (await repository.FindUserAsync(username) is not null)
            throw new ArgumentException("این نام کاربری از قبل هست؛ نام دیگری انتخاب کنید (حساب موجود با این ابزار ارتقا داده نمی‌شود)");
        var user = new User { Username = username, Name = name, LastName = lastName, IsActive = true, CreatedAt = DateTime.UtcNow };
        user.PasswordHash = passwords.Hash(user, password!);
        repository.AddUser(user);
        repository.SetUserRoles(user, [role.RoleId]);
        await repository.SaveAsync();
        return user.UserId;
    }
}
