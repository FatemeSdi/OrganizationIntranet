using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using OrganizationIntranet.Data;
using OrganizationIntranet.Models;

namespace OrganizationIntranet.Pages.Admin.Users
{
    [Authorize(Roles = "ADMIN")]
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _db;

        public IndexModel(AppDbContext db)
        {
            _db = db;
        }

        public List<UserRow> UserRows { get; set; } = new();

        public class UserRow
        {
            public long UserId { get; set; }
            public string Name { get; set; } = string.Empty;
            public string LastName { get; set; } = string.Empty;
            public string Username { get; set; } = string.Empty;
            public bool IsActive { get; set; }
            public DateTime CreatedAt { get; set; }
            public DateTime? LastLogin { get; set; }
            public string Roles { get; set; } = string.Empty;
        }

        public async Task OnGetAsync()
        {
            UserRows = await _db.Users
                .OrderBy(u => u.Name)
                .Select(u => new UserRow
                {
                    UserId = u.UserId,
                    Name = u.Name,
                    LastName = u.LastName,
                    Username = u.Username,
                    IsActive = u.IsActive,
                    CreatedAt = u.CreatedAt,
                    LastLogin = u.LastLogin,
                    Roles = string.Join("، ", u.UserRoles.Select(ur => ur.Role.RoleName)),
                })
                .ToListAsync();
        }
    }
}
