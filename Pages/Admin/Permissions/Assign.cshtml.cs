using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using OrganizationIntranet.Data;
using OrganizationIntranet.Models;

namespace OrganizationIntranet.Pages.Admin.Permissions
{
    [Authorize(Roles = "ADMIN")]
    public class AssignModel : PageModel
    {
        private readonly AppDbContext _db;

        public AssignModel(AppDbContext db)
        {
            _db = db;
        }

        [BindProperty(SupportsGet = true)]
        public int Id { get; set; }

        [BindProperty]
        public List<int> SelectedRoleIds { get; set; } = new();

        public Permission? Permission { get; set; }
        public List<Role> AllRoles { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            Permission = await _db.Permissions
                .Include(p => p.Application)
                .Include(p => p.RolePermissions)
                .FirstOrDefaultAsync(p => p.PermissionId == Id);

            if (Permission == null)
            {
                return NotFound();
            }

            AllRoles = await _db.Roles.Where(r => r.IsActive).OrderBy(r => r.RoleName).ToListAsync();
            SelectedRoleIds = Permission.RolePermissions.Select(rp => rp.RoleId).ToList();

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var permission = await _db.Permissions
                .Include(p => p.Application)
                .Include(p => p.RolePermissions)
                .FirstOrDefaultAsync(p => p.PermissionId == Id);

            if (permission == null)
            {
                return NotFound();
            }

            var currentRoleIds = permission.RolePermissions.Select(rp => rp.RoleId).ToHashSet();
            var toRemove = permission.RolePermissions.Where(rp => !SelectedRoleIds.Contains(rp.RoleId)).ToList();
            _db.RolePermissions.RemoveRange(toRemove);

            foreach (var roleId in SelectedRoleIds.Where(rid => !currentRoleIds.Contains(rid)))
            {
                // ستون CreatedAt این جدول با GETDATE() (زمان محلی) پیش‌فرض شده، نه GETUTCDATE() مثل بقیه؛
                // برای هم‌خوانی با مقدار پیش‌فرض واقعی دیتابیس، اینجا هم DateTime.Now استفاده می‌شود.
                _db.RolePermissions.Add(new RolePermission { RoleId = roleId, PermissionId = permission.PermissionId, CreatedAt = DateTime.Now });
            }

            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = $"نقش‌های Permission «{permission.PermissionName}» به‌روزرسانی شد";
            return RedirectToPage("/Admin/Permissions/Index");
        }
    }
}
