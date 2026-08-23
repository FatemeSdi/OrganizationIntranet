using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using OrganizationIntranet.Data;
using OrganizationIntranet.Models;

namespace OrganizationIntranet.Pages.Admin.Permissions
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
        public string PermissionName { get; set; } = string.Empty;

        [BindProperty]
        public string PermissionCode { get; set; } = string.Empty;

        [BindProperty]
        public int ApplicationId { get; set; }

        [BindProperty]
        public string? Description { get; set; }

        public string? ErrorMessage { get; set; }
        public List<Application> AllApplications { get; set; } = new();
        public List<PermissionRow> AllPermissions { get; set; } = new();

        public class PermissionRow
        {
            public int PermissionId { get; set; }
            public string PermissionName { get; set; } = string.Empty;
            public string PermissionCode { get; set; } = string.Empty;
            public string? Description { get; set; }
            public string ApplicationName { get; set; } = string.Empty;
            public bool IsActive { get; set; }
            public int RoleCount { get; set; }
        }

        public async Task OnGetAsync()
        {
            await LoadAsync();
        }

        public async Task<IActionResult> OnPostCreateAsync()
        {
            await LoadAsync();

            if (string.IsNullOrWhiteSpace(PermissionName) || string.IsNullOrWhiteSpace(PermissionCode) || ApplicationId == 0)
            {
                ErrorMessage = "نام، کد Permission و انتخاب سامانه الزامی است";
                return Page();
            }

            _db.Permissions.Add(new Permission
            {
                PermissionName = PermissionName,
                PermissionCode = PermissionCode.ToUpperInvariant(),
                ApplicationId = ApplicationId,
                Description = string.IsNullOrWhiteSpace(Description) ? null : Description,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
            });

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                ErrorMessage = "ذخیره‌سازی ناموفق بود؛ احتمالاً کد Permission برای این سامانه تکراری است";
                return Page();
            }

            TempData["SuccessMessage"] = "Permission جدید ایجاد شد";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostToggleAsync(int permissionId)
        {
            var permission = await _db.Permissions.FindAsync(permissionId);
            if (permission != null)
            {
                permission.IsActive = !permission.IsActive;
                await _db.SaveChangesAsync();
            }

            return RedirectToPage();
        }

        private async Task LoadAsync()
        {
            AllApplications = await _db.Applications.Where(a => a.IsActive).OrderBy(a => a.DisplayOrder).ToListAsync();

            AllPermissions = await _db.Permissions
                .OrderBy(p => p.Application.ApplicationName).ThenBy(p => p.PermissionName)
                .Select(p => new PermissionRow
                {
                    PermissionId = p.PermissionId,
                    PermissionName = p.PermissionName,
                    PermissionCode = p.PermissionCode,
                    Description = p.Description,
                    ApplicationName = p.Application.ApplicationName,
                    IsActive = p.IsActive,
                    RoleCount = p.RolePermissions.Count,
                })
                .ToListAsync();
        }
    }
}
