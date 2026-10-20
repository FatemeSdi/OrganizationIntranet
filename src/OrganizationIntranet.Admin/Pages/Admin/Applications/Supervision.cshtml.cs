using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OrganizationIntranet.Contracts;
using OrganizationIntranet.UI;

namespace OrganizationIntranet.Pages.Admin.Applications;

[Authorize(Roles = "ADMIN")]
public class SupervisionModel(ApiClient api) : PageModel
{
    [BindProperty(SupportsGet = true)] public int Id { get; set; }
    [BindProperty(SupportsGet = true)] public int GroupId { get; set; }
    [BindProperty] public string GroupName { get; set; } = "";
    [BindProperty] public bool IsActive { get; set; } = true;
    [BindProperty] public Guid Revision { get; set; }
    [BindProperty] public List<long> UserIds { get; set; } = new();
    [BindProperty] public List<int> RoleIds { get; set; } = new();
    public string ApplicationName { get; set; } = "";
    public List<SupervisionGroupDto> Groups { get; set; } = new();
    public List<UserDto> Users { get; set; } = new();
    public List<RoleDto> Roles { get; set; } = new();
    public string? ErrorMessage { get; set; }
    private async Task LoadAsync()
    {
        ApplicationName = (await api.GetAsync<ApplicationConfigurationDto>($"api/admin/applications/{Id}")).Application.ApplicationName;
        Groups = await api.GetAsync<List<SupervisionGroupDto>>($"api/admin/supervision/applications/{Id}/groups");
        Users = await api.GetAsync<List<UserDto>>("api/admin/users"); Roles = await api.GetAsync<List<RoleDto>>("api/admin/roles");
    }
    public async Task<IActionResult> OnGetAsync()
    {
        await LoadAsync();
        if (GroupId != 0)
        {
            var group = Groups.SingleOrDefault(g => g.Id == GroupId); if (group is null) return NotFound();
            GroupName = group.Name; IsActive = group.IsActive; Revision = group.Revision; UserIds = group.UserIds; RoleIds = group.RoleIds;
        }
        return Page();
    }
    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) { ErrorMessage = "اطلاعات گروه معتبر نیست"; await LoadAsync(); return Page(); }
        try
        {
            var result = await api.PostAsync<OperationResult>($"api/admin/supervision/applications/{Id}/groups", new SupervisionGroupRequest(GroupId, GroupName, IsActive, UserIds, RoleIds, Revision));
            TempData["SuccessMessage"] = result.Message; return RedirectToPage(new { Id });
        }
        catch (ApiException ex) when ((int)ex.Status is 400 or 409) { ErrorMessage = ex.Message; await LoadAsync(); return Page(); }
    }
}
