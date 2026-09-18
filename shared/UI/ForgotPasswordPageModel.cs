using DNTCaptcha.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OrganizationIntranet.Contracts;

namespace OrganizationIntranet.UI;

public class ForgotPasswordPageModel(ApiClient api, IDNTCaptchaValidatorService captcha) : PageModel
{
    [BindProperty] public string Username { get; set; } = "";
    [BindProperty] public string Code { get; set; } = "";
    [BindProperty] public string NewPassword { get; set; } = "";
    [BindProperty] public string ConfirmNewPassword { get; set; } = "";
    public bool Enabled { get; set; }
    public bool AwaitingCode => TempData.Peek("PasswordResetChallenge") is string;
    public string? Message { get; set; }
    private async Task LoadAsync() => Enabled = (await api.GetAsync<AuthenticationMethodsDto>("api/account/methods")).ForgotPasswordEnabled;
    public async Task OnGetAsync() => await LoadAsync();
    public async Task<IActionResult> OnPostAsync()
    {
        await LoadAsync();
        if (!Enabled) { ModelState.AddModelError("", "بازیابی رمز غیرفعال است"); return Page(); }
        if (!captcha.HasRequestValidCaptchaEntry()) { ModelState.AddModelError("", "کد امنیتی اشتباه است"); return Page(); }
        try
        {
            var result = await api.PostAsync<AuthenticationChallengeDto>("api/account/forgot-password", new ForgotPasswordRequest(Username));
            TempData["PasswordResetChallenge"] = result.ChallengeToken;
            Message = "اگر حساب واجد شرایط باشد، کد به شماره همراه ثبت‌شده ارسال می‌شود. اعتبار کد ۵ دقیقه است.";
        }
        catch (ApiException ex) { ModelState.AddModelError("", ex.Message); }
        return Page();
    }
    public async Task<IActionResult> OnPostResetAsync()
    {
        await LoadAsync();
        if (TempData.Peek("PasswordResetChallenge") is not string token) return RedirectToPage();
        try
        {
            var result = await api.PostAsync<OperationResult>("api/account/reset-password", new CompletePasswordResetRequest(token, Code, NewPassword, ConfirmNewPassword));
            TempData.Remove("PasswordResetChallenge");
            TempData["AuthenticationMessage"] = result.Message;
            return RedirectToPage("/Account/Login");
        }
        catch (ApiException ex) { ModelState.AddModelError("", ex.Message); return Page(); }
    }
}
