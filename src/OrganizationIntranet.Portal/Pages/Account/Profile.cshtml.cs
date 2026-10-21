using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OrganizationIntranet.Contracts;
using OrganizationIntranet.UI;
namespace OrganizationIntranet.Pages.Account;
// تغییر رمز عبور به «تنظیمات حساب» (Account/Settings) منتقل شد.
[Authorize]
public class ProfileModel(ApiClient api) : PageModel
{
    public string DisplayName { get; set; } = string.Empty;
    public string Initials { get; set; } = string.Empty;
    public string RoleNames { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string? NationalId { get; set; }
    public string? MobileNumber { get; set; }
    public string? Email { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLogin { get; set; }

    public async Task OnGetAsync()
    {
        var p = await api.GetAsync<ProfileDto>("api/account/me");
        DisplayName = p.DisplayName; Initials = p.Initials; RoleNames = p.RoleNames; Username = p.Username;
        NationalId = p.NationalId; MobileNumber = p.MobileNumber; Email = p.Email; CreatedAt = p.CreatedAt; LastLogin = p.LastLogin;
    }
}
