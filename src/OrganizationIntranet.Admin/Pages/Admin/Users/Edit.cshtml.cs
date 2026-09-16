using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OrganizationIntranet.Contracts;
using OrganizationIntranet.UI;
using OrganizationIntranet.Helper;

namespace OrganizationIntranet.Pages.Admin.Users;
[Authorize(Roles = "ADMIN")]
public class EditModel(ApiClient api) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public long? Id { get; set; }

    [BindProperty]
    public string Name { get; set; } = string.Empty;

    [BindProperty]
    public string LastName { get; set; } = string.Empty;

    [BindProperty]
    public string Username { get; set; } = string.Empty;

    [BindProperty]
    public string? NationalId { get; set; }

    [BindProperty]
    public string? MobileNumber { get; set; }

    [BindProperty]
    public string? Email { get; set; }

    [BindProperty]
    public bool IsActive { get; set; } = true;

    [BindProperty]
    public string? NewPassword { get; set; }

    [BindProperty]
    public List<int> SelectedRoleIds { get; set; } = new();

    public List<RoleDto> AllRoles { get; set; } = new();
    public string? ErrorMessage { get; set; }
    public bool IsNew => Id is null or 0;



    private async Task LoadRolesAsync() => AllRoles = (await api.GetAsync<List<RoleDto>>("api/admin/roles")).Where(r => r.IsActive).ToList();
    public async Task<IActionResult> OnGetAsync()
    {
        await LoadRolesAsync();
        if (!IsNew)
        {
            var u = await api.GetAsync<UserDto>($"api/admin/users/{Id}");
            Name = u.Name; LastName = u.LastName; Username = u.Username; NationalId = u.NationalId; MobileNumber = u.MobileNumber; Email = u.Email; IsActive = u.IsActive; SelectedRoleIds = u.SelectedRoleIds;
        }
        return Page();
    }
    public async Task<IActionResult> OnPostAsync()
    {
        await LoadRolesAsync();
        try
        {
            var result = await api.PostAsync<OperationResult>("api/admin/users", new UserEditRequest { Id = Id, Name = Name, LastName = LastName, Username = Username, NationalId = NationalId, MobileNumber = MobileNumber, Email = Email, IsActive = IsActive, NewPassword = NewPassword, SelectedRoleIds = SelectedRoleIds });
            TempData["SuccessMessage"] = result.Message;
            return RedirectToPage("/Admin/Users/Index");
        }
        catch (ApiException ex) when ((int)ex.Status is 400 or 409) { ErrorMessage = ex.Message; return Page(); }
    }
}
