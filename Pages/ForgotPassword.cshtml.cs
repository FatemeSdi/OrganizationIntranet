using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using OrganizationIntranet.Data;
using OrganizationIntranet.Models;

namespace OrganizationIntranet.Pages
{
    public class ForgotPasswordModel : PageModel
    {
        private readonly AppDbContext _db;

        public ForgotPasswordModel(AppDbContext db)
        {
            _db = db;
        }

        [BindProperty]
        public string Username { get; set; } = string.Empty;

        [BindProperty]
        public string NationalId { get; set; } = string.Empty;

        [BindProperty]
        public string MobileNumber { get; set; } = string.Empty;

        [BindProperty]
        public string NewPassword { get; set; } = string.Empty;

        [BindProperty]
        public string ConfirmNewPassword { get; set; } = string.Empty;

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(NationalId) || string.IsNullOrWhiteSpace(MobileNumber))
            {
                return new JsonResult(new { success = false, message = "همه فیلدهای هویتی را وارد کنید" });
            }

            if (string.IsNullOrWhiteSpace(NewPassword) || NewPassword != ConfirmNewPassword)
            {
                return new JsonResult(new { success = false, message = "رمز عبور جدید و تأیید آن یکسان نیستند" });
            }

            var user = await _db.Users.FirstOrDefaultAsync(u =>
                u.Username == Username &&
                u.NationalId == NationalId &&
                u.MobileNumber == MobileNumber);

            if (user == null || !user.IsActive)
            {
                return new JsonResult(new { success = false, message = "اطلاعات هویتی با کاربری در سامانه مطابقت ندارد" });
            }

            var hasher = new PasswordHasher<User>();
            user.PasswordHash = hasher.HashPassword(user, NewPassword);
            await _db.SaveChangesAsync();

            return new JsonResult(new { success = true, message = "رمز عبور با موفقیت تغییر کرد", redirectUrl = Url.Page("/Login") });
        }
    }
}
