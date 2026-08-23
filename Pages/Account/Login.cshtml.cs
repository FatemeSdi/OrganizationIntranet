using System.Security.Claims;
using DNTCaptcha.Core;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using OrganizationIntranet.Authorization;
using OrganizationIntranet.Data;
using OrganizationIntranet.Models;

namespace OrganizationIntranet.Pages.Account
{
    public class LoginModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly IDNTCaptchaValidatorService _captchaValidatorService;

        public LoginModel(AppDbContext db, IDNTCaptchaValidatorService captchaValidatorService)
        {
            _db = db;
            _captchaValidatorService = captchaValidatorService;
        }

        [BindProperty]
        public string Username { get; set; } = string.Empty;

        [BindProperty]
        public string Password { get; set; } = string.Empty;

        [BindProperty]
        public bool RememberMe { get; set; }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!_captchaValidatorService.HasRequestValidCaptchaEntry())
            {
                return new JsonResult(new { success = false, message = "کد امنیتی اشتباه است" });
            }

            if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
            {
                return new JsonResult(new { success = false, message = "نام کاربری و رمز عبور را وارد کنید" });
            }

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == Username);

            if (user == null || !user.IsActive)
            {
                return new JsonResult(new { success = false, message = "نام کاربری یا رمز عبور نامعتبر است" });
            }

            var hasher = new PasswordHasher<User>();

            if (string.IsNullOrEmpty(user.PasswordHash))
            {
                // کاربری که هنوز رمز عبوری برایش ثبت نشده: همین ورود اول، رمز واردشده را به عنوان رمز او ثبت می‌کند
                user.PasswordHash = hasher.HashPassword(user, Password);
            }
            else if (hasher.VerifyHashedPassword(user, user.PasswordHash, Password) == PasswordVerificationResult.Failed)
            {
                return new JsonResult(new { success = false, message = "نام کاربری یا رمز عبور نامعتبر است" });
            }

            user.LastLogin = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            var roleCodes = await _db.UserRoles
                .Where(ur => ur.UserId == user.UserId)
                .Select(ur => ur.Role.RoleCode)
                .ToListAsync();

            // کدهای Permission ای که از طریق نقش‌های کاربر به او می‌رسند (User → Role → RolePermission → Permission).
            var permissionCodes = await _db.UserRoles
                .Where(ur => ur.UserId == user.UserId)
                .SelectMany(ur => ur.Role.RolePermissions)
                .Where(rp => rp.Permission.IsActive)
                .Select(rp => rp.Permission.PermissionCode)
                .Distinct()
                .ToListAsync();

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new(ClaimTypes.Name, user.Username),
                new(ClaimTypes.GivenName, user.Name),
                new(ClaimTypes.Surname, user.LastName),
            };
            claims.AddRange(roleCodes.Select(r => new Claim(ClaimTypes.Role, r)));
            claims.AddRange(permissionCodes.Select(p => new Claim(PermissionClaimTypes.Permission, p)));

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity),
                new AuthenticationProperties { IsPersistent = RememberMe });

            var redirectPage = roleCodes.Contains("ADMIN") ? "/Admin/Index" : "/Portal/Index";
            return new JsonResult(new { success = true, redirectUrl = Url.Page(redirectPage) });
        }
    }
}
