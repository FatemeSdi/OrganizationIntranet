using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OrganizationIntranet.Contracts;
using OrganizationIntranet.UI;

namespace OrganizationIntranet.Pages.Admin.Applications;

/// <summary>ویرایش کامل یک سامانه: مشخصات، حالت دسترسی (با تخصیص یا درخواست، عمومی، بر اساس چارت)، کاربران و نقش‌ها، واحدها و سرویس‌ها؛ مجوز APPLICATION_VIEW و (برای ذخیره) APPLICATION_EDIT.</summary>
[Authorize(Policy = OrganizationIntranet.Contracts.AdminPermissions.ApplicationViewPolicy), Authorize(Policy = OrganizationIntranet.Contracts.AdminPermissions.UserViewPolicy), OrganizationIntranet.Admin.Authorization.PostRequiresPermission(OrganizationIntranet.Contracts.AdminPermissions.ApplicationEdit)]
public class EditModel(ApiClient api) : PageModel
{
    [BindProperty(SupportsGet = true)] public int Id { get; set; }
    [BindProperty] public ApplicationInput Input { get; set; } = new();
    public List<UserDto> Users { get; set; } = new();
    public List<RoleDto> Roles { get; set; } = new();
    public List<OrgUnitDto> Units { get; set; } = new();
    public string? ErrorMessage { get; set; }
    public List<ApplicationServiceSyncDto> SyncStatuses { get; set; } = new();
    /// <summary>تصویر کارت که در فرم بارگذاری می‌شود؛ نوع از امضای فایل در API تشخیص داده می‌شود.</summary>
    [BindProperty] public IFormFile? ImageFile { get; set; }
    /// <summary>حذف تصویر فعلی کارت (کارت دوباره آیکن را نشان می‌دهد).</summary>
    [BindProperty] public bool RemoveImage { get; set; }
    /// <summary>نسخه‌ی تصویر فعلی برای نشانی پیش‌نمایش؛ ۰ یعنی تصویر ندارد.</summary>
    public long ImageVersion { get; set; }
    private async Task LoadImageVersionAsync() => ImageVersion = (await api.GetAsync<ApplicationConfigurationDto>($"api/admin/applications/{Id}")).Application.ImageVersion;
    private async Task LoadPrincipalsAsync()
    {
        Users = await api.GetAsync<List<UserDto>>("api/admin/users");
        Roles = await api.GetAsync<List<RoleDto>>("api/admin/roles");
        Units = await api.GetAsync<List<OrgUnitDto>>("api/admin/org/units");
    }
    public async Task OnGetAsync()
    {
        var data = await api.GetAsync<ApplicationConfigurationDto>($"api/admin/applications/{Id}");
        var a = data.Application;
        SyncStatuses = data.SyncStatuses ?? new();
        ImageVersion = a.ImageVersion;
        Input = new() { ApplicationName = a.ApplicationName, ApplicationCode = a.ApplicationCode, BaseUrl = a.BaseUrl,
            Description = a.Description, DisplayOrder = a.DisplayOrder, Icon = ApplicationCatalogOptions.Icons.Contains(a.Icon) ? a.Icon! : "apps",
            AccessRequestNotificationChannels = a.AccessRequestNotificationChannels, RequiresLogin = a.RequiresLogin, IsInternetAccessible = a.IsInternetAccessible, AccessMode = a.IsPublic ? "Public" : a.IsUnitRestricted ? "Unit" : "Request",
            UserIds = data.UserIds, RoleIds = data.RoleIds, UnitIds = data.UnitIds ?? new(),
            Services = data.Services.Select(s => new ServiceInput { ServiceCode = s.ServiceCode, DisplayName = s.DisplayName, EndpointUrl = s.EndpointUrl, IsActive = s.IsActive }).ToList() };
        Input.Services.Add(new());
        await LoadPrincipalsAsync();
    }
    /// <summary>ثبت لحظه‌ای افزودن/برداشتن یک کاربر یا نقش از دسترسی سامانه (از admin-pickers.js)؛ بقیه‌ی تنظیمات صفحه ذخیره نمی‌شوند.</summary>
    public async Task<IActionResult> OnPostAccessAsync([FromBody] AccessToggleBody body)
    {
        try
        {
            var request = body.Kind == "role" ? new AccessToggleRequest(null, checked((int)body.Id), body.Granted) : new AccessToggleRequest(body.Id, null, body.Granted);
            var result = await api.PostAsync<OperationResult>($"api/admin/applications/{Id}/access", request);
            return new JsonResult(new { ok = true, message = result.Message });
        }
        catch (ApiException ex) when ((int)ex.Status is 400 or 404 or 409) { return new JsonResult(new { ok = false, message = ex.Message }) { StatusCode = 400 }; }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (ImageFile is { Length: > ApplicationImageRules.MaxBytes }) ModelState.AddModelError(nameof(ImageFile), "حجم تصویر باید حداکثر ۲ مگابایت باشد");
        if (!ModelState.IsValid) { ErrorMessage = "مقادیر واردشده معتبر نیست"; await LoadPrincipalsAsync(); await LoadImageVersionAsync(); return Page(); }
        try
        {
            var request = new ApplicationConfigurationRequest(new(Input.ApplicationName, Input.ApplicationCode, Input.BaseUrl,
                Input.Description, Input.DisplayOrder, Input.Icon, Input.RequiresLogin, Input.IsInternetAccessible, Input.AccessMode == "Public", Input.AccessRequestNotificationChannels, Input.AccessMode == "Unit"),
                Input.UserIds, Input.RoleIds, Input.Services.Where(s => !s.Remove &&
                    !(string.IsNullOrWhiteSpace(s.ServiceCode) && string.IsNullOrWhiteSpace(s.DisplayName) && string.IsNullOrWhiteSpace(s.EndpointUrl)))
                .Select(s => new ApplicationServiceRequest(s.ServiceCode ?? "", s.DisplayName ?? "", s.EndpointUrl, s.IsActive)).ToList(), Input.UnitIds);
            var result = await api.PostAsync<OperationResult>($"api/admin/applications/{Id}", request);
            TempData["SuccessMessage"] = result.Message;
            // تصویر بعد از تنظیمات ذخیره می‌شود؛ رد شدن تصویر تنظیمات ذخیره‌شده را برنمی‌گرداند و پیام جدا می‌دهد.
            try
            {
                if (ImageFile is { Length: > 0 })
                {
                    using var buffer = new MemoryStream();
                    await ImageFile.CopyToAsync(buffer);
                    await api.PostAsync<OperationResult>($"api/admin/applications/{Id}/image", new ApplicationImageUpload(Convert.ToBase64String(buffer.ToArray())));
                }
                else if (RemoveImage) await api.PostAsync<OperationResult>($"api/admin/applications/{Id}/image/remove");
            }
            catch (ApiException ex) when ((int)ex.Status is 400 or 413) { TempData["ImageError"] = $"تنظیمات ذخیره شد، اما تصویر پذیرفته نشد: {ex.Message}"; }
            return RedirectToPage(new { Id });
        }
        catch (ApiException ex) when ((int)ex.Status is 400 or 409)
        { ErrorMessage = ex.Message; await LoadPrincipalsAsync(); await LoadImageVersionAsync(); return Page(); }
    }

    /// <summary>بدنه‌ی درخواست ثبت لحظه‌ای دسترسی (کاربر یا نقش) از انتخاب‌گر admin-pickers.js.</summary>
    public sealed record AccessToggleBody(string Kind, long Id, bool Granted);

    /// <summary>فیلدهای فرم ویرایش سامانه (مشخصات، حالت دسترسی، کاربران، نقش‌ها، واحدها و سرویس‌ها).</summary>
    public sealed class ApplicationInput
    {
        public string ApplicationName { get; set; } = "";
        public string ApplicationCode { get; set; } = "";
        public string? BaseUrl { get; set; }
        public string? Description { get; set; }
        public int? DisplayOrder { get; set; }
        public string AccessRequestNotificationChannels { get; set; } = "InApp";
        public string Icon { get; set; } = "apps";
        public bool RequiresLogin { get; set; } = true;
        public bool IsInternetAccessible { get; set; }
        /// <summary>Request (با تخصیص یا درخواست)، Public (عمومی) یا Unit (بر اساس چارت سازمانی).</summary>
        public string AccessMode { get; set; } = "Request";
        public List<int> UnitIds { get; set; } = new();
        public List<long> UserIds { get; set; } = new();
        public List<int> RoleIds { get; set; } = new();
        public List<ServiceInput> Services { get; set; } = new();
    }
    /// <summary>یک ردیف سرویس سامانه در فرم (کد، نام، نشانی و فعال بودن)؛ Remove یعنی ردیف از فهرست برداشته شود.</summary>
    public sealed class ServiceInput
    {
        public string? ServiceCode { get; set; }
        public string? DisplayName { get; set; }
        public string? EndpointUrl { get; set; }
        public bool IsActive { get; set; } = true;
        public bool Remove { get; set; }
    }
}
