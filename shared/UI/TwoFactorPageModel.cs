using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OrganizationIntranet.Contracts;

namespace OrganizationIntranet.UI;

public class TwoFactorPageModel(ApiClient api, IConfiguration configuration) : PageModel
{
    [BindProperty] public string Method { get; set; } = "authenticator";
    [BindProperty] public string Code { get; set; } = "";
    public PendingLogin? Pending { get; private set; }
    public string? Message { get; set; }
    private bool Load()
    {
        var value = TempData.Peek("PendingLogin") as string;
        Pending = value is null ? null : JsonSerializer.Deserialize<PendingLogin>(value);
        return Pending is not null;
    }
    public IActionResult OnGet()
    {
        if (!Load()) return RedirectToPage("/Account/Login");
        Method = Pending!.Methods.FirstOrDefault() ?? "authenticator";
        return Page();
    }
    public async Task<IActionResult> OnPostAsync()
    {
        if (!Load()) return RedirectToPage("/Account/Login");
        try
        {
            var token = await api.PostAsync<TokenResponse>("api/account/verify-login", new VerifyLoginRequest(Pending!.ChallengeToken, Method, Code));
            var redirect = await AccountSignIn.CompleteAsync(this, api, configuration, token, Pending.RememberMe, Pending.ReturnUrl);
            TempData.Remove("PendingLogin");
            return Redirect(redirect);
        }
        catch (ApiException ex) { ModelState.AddModelError("", ex.Message); return Page(); }
    }
    public async Task<IActionResult> OnPostSendSmsAsync()
    {
        if (!Load()) return RedirectToPage("/Account/Login");
        try { Message = (await api.PostAsync<OperationResult>("api/account/send-login-sms", new SendLoginSmsRequest(Pending!.ChallengeToken))).Message; }
        catch (ApiException ex) { ModelState.AddModelError("", ex.Message); }
        Method = "sms";
        return Page();
    }
}
