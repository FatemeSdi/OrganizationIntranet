using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OrganizationIntranet.Contracts;
using OrganizationIntranet.UI;
using OrganizationIntranet.Helper;

namespace OrganizationIntranet.Pages.Account;

public class ForgotPasswordModel(ApiClient api) : PageModel
{
    [BindProperty]
    public string Username { get; set; } = string.Empty;

    [BindProperty]
    public string NationalId { get; set; } = string.Empty;

    [BindProperty]
    public string MobileNumber { get; set; } = string.Empty;

    [BindProperty]
    public string NewPassword { get; set; } = string.Empty;

    [BindProperty]
    public string ConfirmNewPassword { get; set; } = string.Empty;



    public void OnGet() { }
    public async Task<IActionResult> OnPostAsync()
    {
        try
        {
            var result = await api.PostAsync<OperationResult>("api/account/forgot-password", new ResetPasswordRequest(Username, NationalId, MobileNumber, NewPassword, ConfirmNewPassword));
            return new JsonResult(new { success = result.Success, message = result.Message, redirectUrl = Url.Page("/Account/Login") });
        }
        catch (ApiException ex) { return new JsonResult(new { success = false, message = ex.Message }); }
    }
}
