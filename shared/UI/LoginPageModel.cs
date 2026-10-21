using System.Text.Json;
using DNTCaptcha.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OrganizationIntranet.Contracts;

namespace OrganizationIntranet.UI;

public class LoginPageModel(ApiClient api, IDNTCaptchaValidatorService captcha, IConfiguration configuration) : PageModel
{
    // The national-ID challenge token stays on the server (protected TempData cookie) and never reaches browser JavaScript.
    private const string NationalIdPendingKey = "NationalIdLogin";
    [BindProperty] public string Username { get; set; } = "";
    [BindProperty] public string Password { get; set; } = "";
    [BindProperty] public string Method { get; set; } = LoginMethods.Password;
    [BindProperty] public string NationalId { get; set; } = "";
    [BindProperty] public string Code { get; set; } = "";
    [BindProperty] public bool RememberMe { get; set; }
    [BindProperty(SupportsGet = true)] public bool LoggedOut { get; set; }
    [BindProperty(SupportsGet = true)] public string? ReturnUrl { get; set; }
    public AuthenticationMethodsDto Methods { get; set; } = new(false, false, false, false, false, false);
    public bool AnyMethodEnabled => Methods.PasswordEnabled || Methods.LdapEnabled || Methods.NationalIdSmsEnabled;
    public string? Error { get; set; }
    public async Task OnGetAsync()
    {
        try { Methods = await api.GetAsync<AuthenticationMethodsDto>("api/account/methods"); }
        catch (ApiException ex) { Error = ex.Message; }
        if (!Methods.PasswordEnabled && Methods.LdapEnabled) Method = LoginMethods.Ldap;
        else if (!Methods.PasswordEnabled && !Methods.LdapEnabled && Methods.NationalIdSmsEnabled) Method = LoginMethods.NationalIdSms;
    }
    public async Task<IActionResult> OnPostAsync()
    {
        if (!captcha.HasRequestValidCaptchaEntry()) return new JsonResult(new { success = false, message = "کد امنیتی اشتباه است" });
        try
        {
            var token = await api.PostAsync<TokenResponse>("api/account/login", new LoginRequest(Username, Password, Method));
            if (token.ChallengeToken is not null)
            {
                TempData["PendingLogin"] = JsonSerializer.Serialize(new PendingLogin(token.ChallengeToken, token.Methods ?? [], RememberMe, ReturnUrl));
                return new JsonResult(new { success = true, redirectUrl = Url.Page("/Account/TwoFactor") });
            }
            var redirectUrl = await AccountSignIn.CompleteAsync(this, api, configuration, token, RememberMe, ReturnUrl);
            return new JsonResult(new { success = true, redirectUrl });
        }
        catch (ApiException ex) { return new JsonResult(new { success = false, message = ex.Message }); }
    }
    /// <summary>گام اول ورود با کد ملی: CAPTCHA، سپس درخواست ارسال کد. پاسخ برای کد ملی ثبت‌نشده هم یکسان است.</summary>
    public async Task<IActionResult> OnPostSendCodeAsync()
    {
        if (!captcha.HasRequestValidCaptchaEntry()) return new JsonResult(new { success = false, message = "کد امنیتی اشتباه است" });
        try
        {
            var challenge = await api.PostAsync<AuthenticationChallengeDto>("api/account/national-id/start", new NationalIdLoginRequest(NationalId));
            TempData[NationalIdPendingKey] = JsonSerializer.Serialize(new PendingLogin(challenge.ChallengeToken, challenge.Methods, RememberMe, ReturnUrl));
            return new JsonResult(new { success = true, resendIn = 60, message = "اگر کد ملی ثبت شده باشد، کد تأیید به شماره همراه ثبت‌شده پیامک می‌شود." });
        }
        catch (ApiException ex) { return new JsonResult(new { success = false, message = ex.Message }); }
    }
    public async Task<IActionResult> OnPostResendCodeAsync()
    {
        if (Pending() is not { } pending) return Expired();
        try
        {
            var result = await api.PostAsync<OperationResult>("api/account/national-id/resend", new SendLoginSmsRequest(pending.ChallengeToken));
            return new JsonResult(new { success = true, resendIn = 60, message = result.Message });
        }
        catch (ApiException ex) { return new JsonResult(new { success = false, message = ex.Message }); }
    }
    public async Task<IActionResult> OnPostVerifyCodeAsync()
    {
        if (Pending() is not { } pending) return Expired();
        try
        {
            var token = await api.PostAsync<TokenResponse>("api/account/national-id/verify", new VerifyNationalIdLoginRequest(pending.ChallengeToken, Code));
            var redirectUrl = await AccountSignIn.CompleteAsync(this, api, configuration, token, pending.RememberMe, pending.ReturnUrl);
            TempData.Remove(NationalIdPendingKey);
            return new JsonResult(new { success = true, redirectUrl });
        }
        catch (ApiException ex) { return new JsonResult(new { success = false, message = ex.Message }); }
    }
    private PendingLogin? Pending() => TempData.Peek(NationalIdPendingKey) is string value ? JsonSerializer.Deserialize<PendingLogin>(value) : null;
    private static JsonResult Expired() => new(new { success = false, restart = true, message = "مهلت ورود با کد ملی تمام شده است؛ دوباره کد بگیرید." });
}
