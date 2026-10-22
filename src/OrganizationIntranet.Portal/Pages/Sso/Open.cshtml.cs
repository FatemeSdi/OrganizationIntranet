using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OrganizationIntranet.Contracts;
using OrganizationIntranet.UI;
namespace OrganizationIntranet.Pages.Sso;

/// <summary>
/// کلیک روی کارت سامانه‌ی دارای SSO: از API بلیت ۶۰ثانیه‌ای می‌گیرد و مرورگر را با آن به سامانه می‌فرستد.
/// GET است تا کارت یک پیوند ساده بماند؛ بلیت فقط به مرورگر همین کاربر و فقط به آدرس ثبت‌شده‌ی سامانه می‌رود.
/// </summary>
[Authorize]
public class OpenModel(ApiClient api) : PageModel
{
    public string? Error { get; private set; }

    public async Task<IActionResult> OnGetAsync(int app)
    {
        try { return Redirect((await api.PostAsync<SsoStartDto>($"api/portal/applications/{app}/sso")).Url); }
        catch (ApiException ex) when ((int)ex.Status == 404) { Error = "این سامانه برای شما در دسترس نیست."; }
        catch (ApiException ex) when ((int)ex.Status == 400) { Error = ex.Message; }
        Response.StatusCode = 400;
        return Page();
    }
}
