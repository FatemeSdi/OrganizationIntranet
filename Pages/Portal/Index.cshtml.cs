using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using OrganizationIntranet.Data;

namespace OrganizationIntranet.Pages.Portal
{
    [Authorize]
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _db;

        public IndexModel(AppDbContext db)
        {
            _db = db;
        }

        public string DisplayName { get; set; } = string.Empty;
        public string RoleNames { get; set; } = string.Empty;
        public string Initials { get; set; } = string.Empty;

        public async Task OnGetAsync()
        {
            var name = User.FindFirstValue(ClaimTypes.GivenName) ?? string.Empty;
            var lastName = User.FindFirstValue(ClaimTypes.Surname) ?? string.Empty;
            DisplayName = $"{name} {lastName}".Trim();
            Initials = (name.Length > 0 ? name[..1] : "") + (lastName.Length > 0 ? lastName[..1] : "");

            var userId = long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var roleNames = await _db.UserRoles
                .Where(ur => ur.UserId == userId)
                .Select(ur => ur.Role.RoleName)
                .ToListAsync();
            RoleNames = roleNames.Count > 0 ? string.Join("، ", roleNames) : "بدون نقش";
        }
    }
}
