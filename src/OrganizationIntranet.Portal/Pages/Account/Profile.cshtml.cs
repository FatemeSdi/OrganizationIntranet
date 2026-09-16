using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OrganizationIntranet.Contracts;
using OrganizationIntranet.UI;
using OrganizationIntranet.Helper;
namespace OrganizationIntranet.Pages.Account;
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

    [BindProperty]
    public string CurrentPassword { get; set; } = string.Empty;

    [BindProperty]
    public string NewPassword { get; set; } = string.Empty;

    [BindProperty]
    public string ConfirmNewPassword { get; set; } = string.Empty;


    public async Task OnGetAsync()
    {
        var p = await api.GetAsync<ProfileDto>("api/account/me");
        DisplayName = p.DisplayName; Initials = p.Initials; RoleNames = p.RoleNames; Username = p.Username;
        NationalId = p.NationalId; MobileNumber = p.MobileNumber; Email = p.Email; CreatedAt = p.CreatedAt; LastLogin = p.LastLogin;
    }
    public async Task<IActionResult> OnPostChangePasswordAsync()
    {
        try { return new JsonResult(await api.PostAsync<OperationResult>("api/account/change-password", new ChangePasswordRequest(CurrentPassword, NewPassword, ConfirmNewPassword))); }
        catch (ApiException ex) when (ex.Status != System.Net.HttpStatusCode.Unauthorized && ex.Status != System.Net.HttpStatusCode.Forbidden)
        { return new JsonResult(new { success = false, message = ex.Message }); }
    }
}
