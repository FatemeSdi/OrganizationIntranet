using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using OrganizationIntranet.Data;
using OrganizationIntranet.Models;

namespace OrganizationIntranet.Pages.Admin.Users
{
    [Authorize(Roles = "ADMIN")]
    public class EditModel : PageModel
    {
        private readonly AppDbContext _db;

        public EditModel(AppDbContext db)
        {
            _db = db;
        }

        [BindProperty(SupportsGet = true)]
        public long? Id { get; set; }

        [BindProperty]
        public string Name { get; set; } = string.Empty;

        [BindProperty]
        public string LastName { get; set; } = string.Empty;

        [BindProperty]
        public string Username { get; set; } = string.Empty;

        [BindProperty]
        public string? NationalId { get; set; }

        [BindProperty]
        public string? MobileNumber { get; set; }

        [BindProperty]
        public string? Email { get; set; }

        [BindProperty]
        public bool IsActive { get; set; } = true;

        [BindProperty]
        public string? NewPassword { get; set; }

        [BindProperty]
        public List<int> SelectedRoleIds { get; set; } = new();

        public List<Role> AllRoles { get; set; } = new();
        public string? ErrorMessage { get; set; }
        public bool IsNew => Id is null or 0;

        public async Task<IActionResult> OnGetAsync()
        {
            AllRoles = await _db.Roles.Where(r => r.IsActive).OrderBy(r => r.RoleName).ToListAsync();

            if (!IsNew)
            {
                var user = await _db.Users
                    .Include(u => u.UserRoles)
                    .FirstOrDefaultAsync(u => u.UserId == Id);

                if (user == null)
                {
                    return NotFound();
                }

                Name = user.Name;
                LastName = user.LastName;
                Username = user.Username;
                NationalId = user.NationalId;
                MobileNumber = user.MobileNumber;
                Email = user.Email;
                IsActive = user.IsActive;
                SelectedRoleIds = user.UserRoles.Select(ur => ur.RoleId).ToList();
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            AllRoles = await _db.Roles.Where(r => r.IsActive).OrderBy(r => r.RoleName).ToListAsync();

            if (string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(LastName) || string.IsNullOrWhiteSpace(Username))
            {
                ErrorMessage = "نام، نام خانوادگی و نام کاربری الزامی است";
                return Page();
            }

            User user;
            if (IsNew)
            {
                if (string.IsNullOrWhiteSpace(NewPassword))
                {
                    ErrorMessage = "برای کاربر جدید، رمز عبور الزامی است";
                    return Page();
                }

                user = new User { CreatedAt = DateTime.UtcNow };
                _db.Users.Add(user);
            }
            else
            {
                var existing = await _db.Users
                    .Include(u => u.UserRoles)
                    .FirstOrDefaultAsync(u => u.UserId == Id);

                if (existing == null)
                {
                    return NotFound();
                }

                user = existing;
            }

            user.Name = Name;
            user.LastName = LastName;
            user.Username = Username;
            user.NationalId = string.IsNullOrWhiteSpace(NationalId) ? null : NationalId;
            user.MobileNumber = string.IsNullOrWhiteSpace(MobileNumber) ? null : MobileNumber;
            user.Email = string.IsNullOrWhiteSpace(Email) ? null : Email;
            user.IsActive = IsActive;

            if (!string.IsNullOrWhiteSpace(NewPassword))
            {
                var hasher = new PasswordHasher<User>();
                user.PasswordHash = hasher.HashPassword(user, NewPassword);
            }

            try
            {
                if (IsNew)
                {
                    // برای دسترسی به UserId، ابتدا کاربر ذخیره می‌شود و سپس نقش‌ها اضافه می‌شوند
                    await _db.SaveChangesAsync();
                    foreach (var roleId in SelectedRoleIds)
                    {
                        _db.UserRoles.Add(new UserRole { UserId = user.UserId, RoleId = roleId, CreatedAt = DateTime.UtcNow });
                    }
                }
                else
                {
                    var currentRoleIds = user.UserRoles.Select(ur => ur.RoleId).ToHashSet();
                    var toRemove = user.UserRoles.Where(ur => !SelectedRoleIds.Contains(ur.RoleId)).ToList();
                    _db.UserRoles.RemoveRange(toRemove);

                    foreach (var roleId in SelectedRoleIds.Where(rid => !currentRoleIds.Contains(rid)))
                    {
                        _db.UserRoles.Add(new UserRole { UserId = user.UserId, RoleId = roleId, CreatedAt = DateTime.UtcNow });
                    }
                }

                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                ErrorMessage = "ذخیره‌سازی ناموفق بود؛ احتمالاً نام کاربری تکراری است";
                return Page();
            }

            TempData["SuccessMessage"] = IsNew ? "کاربر با موفقیت ایجاد شد" : "کاربر با موفقیت به‌روزرسانی شد";
            return RedirectToPage("/Admin/Users/Index");
        }
    }
}
