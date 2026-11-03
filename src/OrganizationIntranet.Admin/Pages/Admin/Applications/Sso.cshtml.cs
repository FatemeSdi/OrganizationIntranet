using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OrganizationIntranet.Contracts;
using OrganizationIntranet.UI;

namespace OrganizationIntranet.Pages.Admin.Applications;

/// <summary>
/// تنظیمات ورود یکپارچه‌ی یک سامانه: مسیر صفحه‌ی SSO و کلید. کلید فقط به‌صورت هش ذخیره می‌شود؛ کلید ساخته‌شده فقط یک بار، همین‌جا بعد از ساخت، نشان داده می‌شود
/// (در TempData نمی‌ماند و بعد از بستن صفحه قابل خواندن نیست). مشاهده با APPLICATION_VIEW و تغییر با APPLICATION_EDIT.
/// </summary>
[Authorize(Policy = AdminPermissions.ApplicationViewPolicy), OrganizationIntranet.Admin.Authorization.PostRequiresPermission(AdminPermissions.ApplicationEdit)]
public class SsoModel(ApiClient api) : PageModel
{
    [BindProperty(SupportsGet = true)] public int Id { get; set; }
    [BindProperty] public string? SsoPath { get; set; }
    [BindProperty] public string? NewKey { get; set; }
    public SsoSettingsDto Data { get; private set; } = null!;
    /// <summary>کلید تازه‌ساخته؛ فقط در پاسخ همین ذخیره پر است.</summary>
    public string? GeneratedKey { get; private set; }
    public string? ErrorMessage { get; private set; }
    public string? SuccessMessage { get; private set; }

    private async Task LoadAsync() => Data = await api.GetAsync<SsoSettingsDto>($"api/admin/applications/{Id}/sso");
    public async Task OnGetAsync() { await LoadAsync(); SsoPath = Data.SsoPath; }

    /// <summary>ذخیره‌ی مسیر بدون تغییر کلید.</summary>
    public Task<IActionResult> OnPostSaveAsync() => SubmitAsync(SsoKeyActions.Keep);
    /// <summary>ساخت کلید تصادفی تازه (کلید قبلی از همین لحظه کار نمی‌کند) و نمایش یک‌باره‌ی آن.</summary>
    public Task<IActionResult> OnPostGenerateAsync() => SubmitAsync(SsoKeyActions.Generate);
    /// <summary>ثبت کلیدی که سامانه از قبل دارد (مثلاً کلید فعلی TaskTrack)، بدون تغییر در خود سامانه.</summary>
    public Task<IActionResult> OnPostSetKeyAsync() => SubmitAsync(SsoKeyActions.Set);
    /// <summary>حذف کلید؛ ورود یکپارچه‌ی این سامانه تا ثبت کلید تازه غیرفعال می‌شود.</summary>
    public Task<IActionResult> OnPostRemoveKeyAsync() => SubmitAsync(SsoKeyActions.Remove);

    private async Task<IActionResult> SubmitAsync(string action)
    {
        try
        {
            var result = await api.PostAsync<SsoSettingsResultDto>($"api/admin/applications/{Id}/sso", new SsoSettingsRequest(SsoPath, action, NewKey));
            Data = result.Settings; GeneratedKey = result.GeneratedKey; NewKey = null; SsoPath = Data.SsoPath;
            SuccessMessage = action switch
            {
                SsoKeyActions.Generate => "کلید تازه ساخته شد؛ همین حالا آن را در تنظیمات سامانه بگذارید",
                SsoKeyActions.Set => "کلید ثبت شد",
                SsoKeyActions.Remove => "کلید حذف شد؛ ورود یکپارچه‌ی این سامانه غیرفعال است",
                _ => "تنظیمات ذخیره شد",
            };
            return Page();
        }
        catch (ApiException ex) when ((int)ex.Status is 400 or 409) { ErrorMessage = ex.Message; await LoadAsync(); return Page(); }
    }
}
