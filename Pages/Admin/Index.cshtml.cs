using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using OrganizationIntranet.Data;

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

        public async Task OnGetAsync()
        {
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
