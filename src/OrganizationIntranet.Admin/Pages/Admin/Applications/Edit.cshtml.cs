using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OrganizationIntranet.Contracts;
using OrganizationIntranet.UI;

namespace OrganizationIntranet.Pages.Admin.Applications;

[Authorize(Roles = "ADMIN")]
public class EditModel(ApiClient api) : PageModel
{
    [BindProperty(SupportsGet = true)] public int Id { get; set; }
    [BindProperty] public ApplicationInput Input { get; set; } = new();
    public List<UserDto> Users { get; set; } = new();
    public List<RoleDto> Roles { get; set; } = new();
    public string? ErrorMessage { get; set; }
    public List<ApplicationServiceSyncDto> SyncStatuses { get; set; } = new();
    private async Task LoadPrincipalsAsync()
    {
        Users = await api.GetAsync<List<UserDto>>("api/admin/users");
        Roles = await api.GetAsync<List<RoleDto>>("api/admin/roles");
    }
    public async Task OnGetAsync()
    {
        var data = await api.GetAsync<ApplicationConfigurationDto>($"api/admin/applications/{Id}");
        var a = data.Application;
        SyncStatuses = data.SyncStatuses ?? new();
        Input = new() { ApplicationName = a.ApplicationName, ApplicationCode = a.ApplicationCode, BaseUrl = a.BaseUrl,
            Description = a.Description, DisplayOrder = a.DisplayOrder, Icon = ApplicationCatalogOptions.Icons.Contains(a.Icon) ? a.Icon! : "apps",
            AccessRequestNotificationChannels = a.AccessRequestNotificationChannels, RequiresLogin = a.RequiresLogin, IsInternetAccessible = a.IsInternetAccessible, IsPublic = a.IsPublic,
            UserIds = data.UserIds, RoleIds = data.RoleIds,
            Services = data.Services.Select(s => new ServiceInput { ServiceCode = s.ServiceCode, DisplayName = s.DisplayName, EndpointUrl = s.EndpointUrl, IsActive = s.IsActive }).ToList() };
        Input.Services.Add(new());
        await LoadPrincipalsAsync();
    }
    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) { ErrorMessage = "مقادیر واردشده معتبر نیست"; await LoadPrincipalsAsync(); return Page(); }
        try
        {
            var request = new ApplicationConfigurationRequest(new(Input.ApplicationName, Input.ApplicationCode, Input.BaseUrl,
                Input.Description, Input.DisplayOrder, Input.Icon, Input.RequiresLogin, Input.IsInternetAccessible, Input.IsPublic, Input.AccessRequestNotificationChannels),
                Input.UserIds, Input.RoleIds, Input.Services.Where(s => !s.Remove &&
                    !(string.IsNullOrWhiteSpace(s.ServiceCode) && string.IsNullOrWhiteSpace(s.DisplayName) && string.IsNullOrWhiteSpace(s.EndpointUrl)))
                .Select(s => new ApplicationServiceRequest(s.ServiceCode ?? "", s.DisplayName ?? "", s.EndpointUrl, s.IsActive)).ToList());
            var result = await api.PostAsync<OperationResult>($"api/admin/applications/{Id}", request);
            TempData["SuccessMessage"] = result.Message;
            return RedirectToPage(new { Id });
        }
        catch (ApiException ex) when ((int)ex.Status is 400 or 409)
        { ErrorMessage = ex.Message; await LoadPrincipalsAsync(); return Page(); }
    }

    public sealed class ApplicationInput
    {
        public string ApplicationName { get; set; } = "";
        public string ApplicationCode { get; set; } = "";
        public string? BaseUrl { get; set; }
        public string? Description { get; set; }
        public int DisplayOrder { get; set; }
        public string AccessRequestNotificationChannels { get; set; } = "InApp";
        public string Icon { get; set; } = "apps";
        public bool RequiresLogin { get; set; } = true;
        public bool IsInternetAccessible { get; set; }
        public bool IsPublic { get; set; }
        public List<long> UserIds { get; set; } = new();
        public List<int> RoleIds { get; set; } = new();
        public List<ServiceInput> Services { get; set; } = new();
    }
    public sealed class ServiceInput
    {
        public string? ServiceCode { get; set; }
        public string? DisplayName { get; set; }
        public string? EndpointUrl { get; set; }
        public bool IsActive { get; set; } = true;
        public bool Remove { get; set; }
    }
}
