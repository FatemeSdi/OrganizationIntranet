using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OrganizationIntranet.Authorization;
using OrganizationIntranet.Contracts;
using OrganizationIntranet.UI;

namespace OrganizationIntranet.Pages.Admin.Notifications;

/// <summary>
/// ارسال اعلان سازمانی به یک کاربر، یک واحد (با زیرواحدها) یا همه، و فهرست ارسال‌ها با پس‌گرفتن و بازگرداندن.
/// انتخاب کاربر فقط برای کسی هست که «مشاهده‌ی کاربران» را هم دارد. قواعد در API (AdminNotificationService) اعمال می‌شود.
/// </summary>
[Authorize(Policy = AdminPermissions.NotificationSendPolicy), OrganizationIntranet.Admin.Authorization.PostRequiresPermission(AdminPermissions.NotificationSend)]
public class IndexModel(ApiClient api) : PageModel
{
    [BindProperty] public string? Title { get; set; }
    [BindProperty] public string? Message { get; set; }
    [BindProperty] public string AudienceType { get; set; } = AdminNotificationAudience.All;
    [BindProperty] public long? UserId { get; set; }
    [BindProperty] public int? UnitId { get; set; }
    [BindProperty(SupportsGet = true)] public int PageNumber { get; set; } = 1;
    public AdminNotificationListDto Data { get; private set; } = new([], 0, 1, AdminNotificationAudience.PageSize);
    public List<OrgUnitDto> Units { get; private set; } = [];
    public List<UserDto> Users { get; private set; } = [];
    public bool CanPickUser => User.HasPermission(AdminPermissions.UserView);
    public string? ErrorMessage { get; private set; }

    private async Task LoadAsync()
    {
        Data = await api.GetAsync<AdminNotificationListDto>($"api/admin/notifications?page={Math.Max(1, PageNumber)}");
        Units = (await api.GetAsync<List<OrgUnitDto>>("api/admin/org/units")).Where(u => u.IsActive).ToList();
        if (CanPickUser) Users = (await api.GetAsync<List<UserDto>>("api/admin/users")).Where(u => u.IsActive).OrderBy(u => u.LastName).ThenBy(u => u.Name).ToList();
    }
    public async Task OnGetAsync() => await LoadAsync();

    /// <summary>ارسال اعلان؛ پیام موفقیت شمار گیرندگان را می‌گوید.</summary>
    public async Task<IActionResult> OnPostSendAsync()
    {
        try
        {
            var result = await api.PostAsync<OperationResult>("api/admin/notifications", new AdminNotificationRequest(Title, Message, AudienceType, UserId, UnitId));
            TempData["SuccessMessage"] = result.Message;
            return RedirectToPage();
        }
        catch (ApiException ex) when ((int)ex.Status is 400 or 404 or 409) { ErrorMessage = ex.Message; await LoadAsync(); return Page(); }
    }
    /// <summary>پس‌گرفتن (active=false) یا بازگرداندن (active=true) یک اعلان.</summary>
    public async Task<IActionResult> OnPostSetActiveAsync(long id, bool active)
    {
        try
        {
            var result = await api.PostAsync<OperationResult>($"api/admin/notifications/{id}/{(active ? "activate" : "deactivate")}");
            TempData["SuccessMessage"] = result.Message;
            return RedirectToPage(new { pageNumber = PageNumber });
        }
        catch (ApiException ex) when ((int)ex.Status is 400 or 404) { ErrorMessage = ex.Message; await LoadAsync(); return Page(); }
    }
}
