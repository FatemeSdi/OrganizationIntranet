using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using OrganizationIntranet.Data;
using OrganizationIntranet.Models;

namespace OrganizationIntranet.Pages.Admin.Applications
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
        public string ApplicationName { get; set; } = string.Empty;

        [BindProperty]
        public string ApplicationCode { get; set; } = string.Empty;

        [BindProperty]
        public string? BaseUrl { get; set; }

        [BindProperty]
        public string? Description { get; set; }

        [BindProperty]
        public int DisplayOrder { get; set; }

        public string? ErrorMessage { get; set; }
        public List<Application> AllApplications { get; set; } = new();

        public async Task OnGetAsync()
        {
            AllApplications = await _db.Applications.OrderBy(a => a.DisplayOrder).ToListAsync();
        }

        public async Task<IActionResult> OnPostCreateAsync()
        {
            if (string.IsNullOrWhiteSpace(ApplicationName) || string.IsNullOrWhiteSpace(ApplicationCode))
            {
                ErrorMessage = "نام و کد سامانه الزامی است";
                AllApplications = await _db.Applications.OrderBy(a => a.DisplayOrder).ToListAsync();
                return Page();
            }

            _db.Applications.Add(new Application
            {
                ApplicationName = ApplicationName,
                ApplicationCode = ApplicationCode.ToUpperInvariant(),
                BaseUrl = string.IsNullOrWhiteSpace(BaseUrl) ? null : BaseUrl,
                Description = string.IsNullOrWhiteSpace(Description) ? null : Description,
                DisplayOrder = DisplayOrder,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
            });

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                ErrorMessage = "ذخیره‌سازی ناموفق بود؛ احتمالاً کد سامانه تکراری است";
                AllApplications = await _db.Applications.OrderBy(a => a.DisplayOrder).ToListAsync();
                return Page();
            }

            TempData["SuccessMessage"] = "سامانه جدید ایجاد شد";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostToggleAsync(int applicationId)
        {
            var app = await _db.Applications.FindAsync(applicationId);
            if (app != null)
            {
                app.IsActive = !app.IsActive;
                await _db.SaveChangesAsync();
            }

            return RedirectToPage();
        }
    }
}
