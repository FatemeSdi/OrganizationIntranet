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
    /// <summary>تصویر شاخص کارت خبر؛ نوع واقعی فایل در API از روی امضای آن بررسی می‌شود.</summary>
    [BindProperty] public IFormFile? ImageFile { get; set; }
    [BindProperty] public bool RemoveImage { get; set; }
    public long ImageVersion { get; set; }
    public async Task OnGetAsync()
    {
        if (Id.HasValue)
        {
            var p = await api.GetAsync<PublicationDto>($"api/admin/publications/{Id}");
            Input = new() { Title = p.Title, Summary = p.Summary, Body = p.Body, Kind = p.Kind, IsPublished = p.IsPublished, IsPublic = p.IsPublic, Revision = p.Revision };
            ImageVersion = p.ImageVersion;
        }
    }
    public async Task<IActionResult> OnPostAsync()
    {
        if (ImageFile is { Length: > PublicationImageRules.MaxBytes }) ModelState.AddModelError(nameof(ImageFile), "حجم تصویر باید حداکثر ۲ مگابایت باشد");
        if (!ModelState.IsValid) { await LoadImageVersionAsync(); return Page(); }
        PublicationDto saved;
        try
        {
            saved = await api.PostAsync<PublicationDto>(Id.HasValue ? $"api/admin/publications/{Id}" : "api/admin/publications", Input);
        }
        catch (ApiException ex) when ((int)ex.Status is 400 or 409)
        {
            ModelState.AddModelError("", ex.Message);
            await LoadImageVersionAsync();
            return Page();
        }
        TempData["SuccessMessage"] = !Input.IsPublished ? "پیش‌نویس ذخیره شد و در پورتال نمایش داده نمی‌شود."
            : Input.IsPublic ? "محتوا منتشر شد و بدون نیاز به ورود هم نمایش داده می‌شود." : "محتوا برای کاربران واردشده منتشر شد.";
        try
        {
            if (ImageFile is { Length: > 0 })
            {
                using var buffer = new MemoryStream();
                await ImageFile.CopyToAsync(buffer);
                await api.PostAsync<PublicationDto>($"api/admin/publications/{saved.PublicationId}/image", new PublicationImageUpload(Convert.ToBase64String(buffer.ToArray())));
            }
            else if (RemoveImage) await api.PostAsync<OperationResult>($"api/admin/publications/{saved.PublicationId}/image/remove");
        }
        catch (ApiException ex) when ((int)ex.Status is 400 or 413)
        {
            // متن ذخیره شده است؛ مدیر در همان صفحه تصویر مناسب را دوباره انتخاب می‌کند.
            TempData.Remove("SuccessMessage");
            TempData["ImageError"] = $"متن ذخیره شد، اما تصویر پذیرفته نشد: {ex.Message}";
            return RedirectToPage(new { id = saved.PublicationId });
        }
        return RedirectToPage("Index");
    }
    private async Task LoadImageVersionAsync()
    {
        if (Id.HasValue) ImageVersion = (await api.GetAsync<PublicationDto>($"api/admin/publications/{Id}")).ImageVersion;
    }
}
