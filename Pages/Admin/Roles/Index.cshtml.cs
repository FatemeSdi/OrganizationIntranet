using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using OrganizationIntranet.Data;
using OrganizationIntranet.Models;

namespace OrganizationIntranet.Pages.Admin.Roles
{
    [Authorize(Roles = "ADMIN")]
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _db;

        public IndexModel(AppDbContext db)
        {
            _db = db;
        }

        [BindProperty]
        public string RoleName { get; set; } = string.Empty;

        [BindProperty]
        public string RoleCode { get; set; } = string.Empty;

        public string? ErrorMessage { get; set; }
        public List<Role> AllRoles { get; set; } = new();

        public async Task OnGetAsync()
        {
            AllRoles = await _db.Roles.OrderBy(r => r.RoleName).ToListAsync();
        }

        public async Task<IActionResult> OnPostCreateAsync()
        {
            if (string.IsNullOrWhiteSpace(RoleName) || string.IsNullOrWhiteSpace(RoleCode))
            {
                ErrorMessage = "نام و کد نقش الزامی است";
                AllRoles = await _db.Roles.OrderBy(r => r.RoleName).ToListAsync();
                return Page();
            }

            _db.Roles.Add(new Role { RoleName = RoleName, RoleCode = RoleCode.ToUpperInvariant(), IsActive = true, CreatedAt = DateTime.UtcNow });

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                ErrorMessage = "ذخیره‌سازی ناموفق بود؛ احتمالاً کد نقش تکراری است";
                AllRoles = await _db.Roles.OrderBy(r => r.RoleName).ToListAsync();
                return Page();
            }

            TempData["SuccessMessage"] = "نقش جدید ایجاد شد";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostToggleAsync(int roleId)
        {
            var role = await _db.Roles.FindAsync(roleId);
            if (role != null)
            {
                role.IsActive = !role.IsActive;
                await _db.SaveChangesAsync();
            }

            return RedirectToPage();
        }
    }
}
