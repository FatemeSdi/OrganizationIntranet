using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using OrganizationIntranet.Data;
using OrganizationIntranet.Models;

namespace OrganizationIntranet.Pages
{
    [Authorize]
    public class PortalModel : PageModel
    {
        private readonly AppDbContext _db;

        public PortalModel(AppDbContext db)
        {
            _db = db;
        }

        public string DisplayName { get; set; } = string.Empty;
        public string RoleNames { get; set; } = string.Empty;
        public List<Application> Applications { get; set; } = new();
        public int UnreadNotificationCount { get; set; }
        public List<Notification> RecentNotifications { get; set; } = new();

        public async Task OnGetAsync()
        {
            var userId = long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            DisplayName = $"{User.FindFirstValue(ClaimTypes.GivenName)} {User.FindFirstValue(ClaimTypes.Surname)}".Trim();
            RoleNames = string.Join("، ", User.FindAll(ClaimTypes.Role).Select(c => c.Value));

            Applications = await _db.Applications
                .Where(a => a.IsActive)
                .OrderBy(a => a.DisplayOrder)
                .ToListAsync();

            UnreadNotificationCount = await _db.UserNotifications
                .CountAsync(un => un.UserId == userId && !un.IsRead);

            RecentNotifications = await _db.UserNotifications
                .Where(un => un.UserId == userId)
                .OrderByDescending(un => un.CreatedAt)
                .Take(5)
                .Select(un => un.Notification)
                .ToListAsync();
        }
    }
}
