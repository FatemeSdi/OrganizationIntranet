using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using OrganizationIntranet.Data;
using OrganizationIntranet.Helper;

namespace OrganizationIntranet.Pages.Admin
{
    [Authorize(Roles = "ADMIN")]
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _db;

        public IndexModel(AppDbContext db)
        {
            _db = db;
        }

        public int TotalUsers { get; set; }
        public int ActiveUsers { get; set; }
        public int TotalRoles { get; set; }
        public int TotalApplications { get; set; }
        public List<Models.User> RecentUsers { get; set; } = new();
        public string AdminDisplayName { get; set; } = string.Empty;
        public string TodayPersian { get; set; } = string.Empty;

        public async Task OnGetAsync()
        {
            var name = User.FindFirstValue(ClaimTypes.GivenName) ?? string.Empty;
            var lastName = User.FindFirstValue(ClaimTypes.Surname) ?? string.Empty;
            AdminDisplayName = $"{name} {lastName}".Trim();
            TodayPersian = DateTime.Now.ToPersianDate();

            TotalUsers = await _db.Users.CountAsync();
            ActiveUsers = await _db.Users.CountAsync(u => u.IsActive);
            TotalRoles = await _db.Roles.CountAsync();
            TotalApplications = await _db.Applications.CountAsync();

            RecentUsers = await _db.Users
                .OrderByDescending(u => u.CreatedAt)
                .Take(5)
                .ToListAsync();
        }
    }
}
