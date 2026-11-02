using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OrganizationIntranet.Contracts;
using OrganizationIntranet.UI;

namespace OrganizationIntranet.Pages.Admin.Organization;

/// <summary>ویرایش یک واحد: عنوان، ترتیب، فعال‌بودن، جابه‌جایی زیر واحد دیگر و حذف (فقط واحد بدون زیرمجموعه و کاربر).</summary>
[Authorize(Policy = AdminPermissions.OrgViewPolicy), OrganizationIntranet.Admin.Authorization.PostRequiresPermission(AdminPermissions.OrgEdit)]
public class EditModel(ApiClient api) : PageModel
{
    [BindProperty(SupportsGet = true)] public int Id { get; set; }
    [BindProperty] public string? Title { get; set; }
    [BindProperty] public int? ParentId { get; set; }
    [BindProperty] public int DisplayOrder { get; set; }
    [BindProperty] public bool IsActive { get; set; } = true;
    public List<OrgUnitDto> Units { get; private set; } = [];
    public OrgUnitDto Unit { get; private set; } = null!;
    public string? ErrorMessage { get; private set; }

    /// <summary>شناسه‌ی خود واحد و همه‌ی زیرمجموعه‌هایش؛ این‌ها نباید در فهرست «زیر مجموعه‌ی» بیایند.</summary>
    public HashSet<int> SubtreeIds()
    {
        var ids = new HashSet<int> { Id };
        foreach (var u in Units) if (u.ParentId is not null && ids.Contains(u.ParentId.Value)) ids.Add(u.UnitId); // tree order: a parent always precedes its children
        return ids;
    }

    private async Task<bool> LoadAsync()
    {
        Units = await api.GetAsync<List<OrgUnitDto>>("api/admin/org/units");
        var unit = Units.FirstOrDefault(u => u.UnitId == Id);
        if (unit is null) return false;
        Unit = unit;
        return true;
    }
    public async Task<IActionResult> OnGetAsync()
    {
        if (!await LoadAsync()) return NotFound();
        Title = Unit.Title; ParentId = Unit.ParentId; DisplayOrder = Unit.DisplayOrder; IsActive = Unit.IsActive;
        return Page();
    }
    public async Task<IActionResult> OnPostAsync()
    {
        try
        {
            var result = await api.PostAsync<OperationResult>($"api/admin/org/units/{Id}", new OrgUnitRequest(Title, ParentId, DisplayOrder, IsActive));
            TempData["SuccessMessage"] = result.Message;
            return RedirectToPage("/Admin/Organization/Index");
        }
        catch (ApiException ex) when ((int)ex.Status is 400 or 409) { ErrorMessage = ex.Message; return await LoadAsync() ? Page() : NotFound(); }
    }
    public async Task<IActionResult> OnPostDeleteAsync()
    {
        try
        {
            var result = await api.PostAsync<OperationResult>($"api/admin/org/units/{Id}/delete");
            TempData["SuccessMessage"] = result.Message;
            return RedirectToPage("/Admin/Organization/Index");
        }
        catch (ApiException ex) when ((int)ex.Status is 400 or 409)
        {
            ErrorMessage = ex.Message;
            if (!await LoadAsync()) return NotFound();
            Title = Unit.Title; ParentId = Unit.ParentId; DisplayOrder = Unit.DisplayOrder; IsActive = Unit.IsActive;
            return Page();
        }
    }
}
