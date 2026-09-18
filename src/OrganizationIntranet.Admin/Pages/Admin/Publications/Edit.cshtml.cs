using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OrganizationIntranet.Contracts;
using OrganizationIntranet.UI;

namespace OrganizationIntranet.Pages.Admin.Publications;
[Authorize(Roles = "ADMIN")]
public class EditModel(ApiClient api) : PageModel
{
    [BindProperty(SupportsGet = true)] public long? Id { get; set; }
    [BindProperty] public PublicationEditRequest Input { get; set; } = new();
    public async Task OnGetAsync()
    {
        if (Id.HasValue)
        {
            var p = await api.GetAsync<PublicationDto>($"api/admin/publications/{Id}");
            Input = new() { Title = p.Title, Summary = p.Summary, Body = p.Body, Kind = p.Kind, IsPublished = p.IsPublished, Revision = p.Revision };
        }
    }
    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();
        try
        {
            await api.PostAsync<PublicationDto>(Id.HasValue ? $"api/admin/publications/{Id}" : "api/admin/publications", Input);
            TempData["SuccessMessage"] = Input.IsPublished ? "محتوا منتشر شد." : "پیش‌نویس ذخیره شد و در پورتال نمایش داده نمی‌شود.";
            return RedirectToPage("Index");
        }
        catch (ApiException ex) when ((int)ex.Status is 400 or 409)
        {
            ModelState.AddModelError("", ex.Message);
            return Page();
        }
    }
}
