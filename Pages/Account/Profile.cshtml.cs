using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using OrganizationIntranet.Data;
using OrganizationIntranet.Models;

namespace OrganizationIntranet.Pages.Account
{
    [Authorize]
    public class ProfileModel : PageModel
    {
        private readonly AppDbContext _db;

        public ProfileModel(AppDbContext db)
        {
            _db = db;
        }

        public string DisplayName { get; set; } = string.Empty;
        public string Initials { get; set; } = string.Empty;
        public string RoleNames { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string? NationalId { get; set; }
        public string? MobileNumber { get; set; }
        public string? Email { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastLogin { get; set; }

        [BindProperty]
        public string CurrentPassword { get; set; } = string.Empty;

        [BindProperty]
        public string NewPassword { get; set; } = string.Empty;

        [BindProperty]
        public string ConfirmNewPassword { get; set; } = string.Empty;

        public async Task OnGetAsync()
        {
            await LoadProfileAsync();
        }

        public async Task<IActionResult> OnPostChangePasswordAsync()
        {
            var user = await _db.Users.FindAsync(CurrentUserId);
            if (user == null)
            {
                return new JsonResult(new { success = false, message = "کاربر یافت نشد" });
            }

            var hasher = new PasswordHasher<User>();
            if (string.IsNullOrEmpty(user.PasswordHash) ||
                hasher.VerifyHashedPassword(user, user.PasswordHash, CurrentPassword) == PasswordVerificationResult.Failed)
            {
                return new JsonResult(new { success = false, message = "رمز عبور فعلی اشتباه است" });
            }

            if (string.IsNullOrWhiteSpace(NewPassword) || NewPassword != ConfirmNewPassword)
            {
                return new JsonResult(new { success = false, message = "رمز عبور جدید و تأیید آن یکسان نیستند" });
            }

            user.PasswordHash = hasher.HashPassword(user, NewPassword);
            await _db.SaveChangesAsync();

            return new JsonResult(new { success = true, message = "رمز عبور با موفقیت تغییر کرد" });
        }

        private long CurrentUserId => long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        private async Task LoadProfileAsync()
        {
            var user = await _db.Users.FindAsync(CurrentUserId);
            if (user == null)
            {
                return;
            }

            DisplayName = $"{user.Name} {user.LastName}".Trim();
            Initials = string.Concat(user.Name.Take(1), user.LastName.Take(1));
            Username = user.Username;
            NationalId = user.NationalId;
            MobileNumber = user.MobileNumber;
            Email = user.Email;
            CreatedAt = user.CreatedAt;
            LastLogin = user.LastLogin;

            var roleNames = await _db.UserRoles
                .Where(ur => ur.UserId == user.UserId)
                .Select(ur => ur.Role.RoleName)
                .ToListAsync();
            RoleNames = roleNames.Count > 0 ? string.Join("، ", roleNames) : "بدون نقش";
        }
    }
}
