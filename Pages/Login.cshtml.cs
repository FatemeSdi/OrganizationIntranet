using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using OrganizationIntranet.Data;
using OrganizationIntranet.Models;

namespace OrganizationIntranet.Pages
{
    public class LoginModel : PageModel
    {
        private readonly AppDbContext _db;

        public LoginModel(AppDbContext db)
        {
            _db = db;
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

            var roleNames = await _db.UserRoles
                .Where(ur => ur.UserId == user.UserId)
                .Select(ur => ur.Role.RoleName)
                .ToListAsync();

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new(ClaimTypes.Name, user.Username),
                new(ClaimTypes.GivenName, user.Name),
                new(ClaimTypes.Surname, user.LastName),
            };
            claims.AddRange(roleNames.Select(r => new Claim(ClaimTypes.Role, r)));

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity),
                new AuthenticationProperties { IsPersistent = RememberMe });

            return new JsonResult(new { success = true, redirectUrl = Url.Page("/Portal") });
        }
    }
}
