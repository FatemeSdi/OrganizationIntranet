using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OrganizationIntranet.Contracts;
using OrganizationIntranet.UI;

namespace OrganizationIntranet.Pages.Admin.Publications;
[Authorize(Policy = OrganizationIntranet.Contracts.AdminPermissions.PublicationEditPolicy)]
public class EditModel(ApiClient api) : PageModel
{
    [BindProperty(SupportsGet = true)] public long? Id { get; set; }
    [BindProperty] public PublicationEditRequest Input { get; set; } = new();
    /// <summary>تصویر شاخص کارت خبر؛ نوع واقعی فایل در API از روی امضای آن بررسی می‌شود.</summary>
    [BindProperty] public IFormFile? ImageFile { get; set; }
    [BindProperty] public bool RemoveImage { get; set; }
    public long ImageVersion { get; set; }
    public bool IsArchived { get; set; }
    public async Task OnGetAsync()
    {
        if (Id.HasValue)
        {
            var p = await api.GetAsync<PublicationDto>($"api/admin/publications/{Id}");
            Input = new() { Title = p.Title, Summary = p.Summary, Body = p.Body, Kind = p.Kind, IsPublished = p.IsPublished, IsPublic = p.IsPublic, Revision = p.Revision };
            ImageVersion = p.ImageVersion;
            IsArchived = p.IsArchived;
        }
    }
    public async Task<IActionResult> OnPostAsync()
    {
        if (Id.HasValue && (await api.GetAsync<PublicationDto>($"api/admin/publications/{Id}")).IsArchived)
        {
            ModelState.AddModelError("", "مطلب بایگانی‌شده را نمی‌توان ویرایش کرد؛ ابتدا آن را بازگردانی کنید");
            await LoadImageVersionAsync();
            return Page();
        }
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
    /// <summary>بایگانی (حذف نرم): مطلب از پرتال برداشته می‌شود ولی در بانک می‌ماند و با «بازگردانی» به پیش‌نویس برمی‌گردد.</summary>
    public async Task<IActionResult> OnPostArchiveAsync()
    {
        await api.PostAsync<PublicationDto>($"api/admin/publications/{Id}/archive");
        TempData["SuccessMessage"] = "مطلب بایگانی شد و در پورتال نمایش داده نمی‌شود. از فیلتر «بایگانی‌شده» می‌توانید آن را بازگردانی کنید.";
        return RedirectToPage("Index");
    }
    public async Task<IActionResult> OnPostRestoreAsync()
    {
        await api.PostAsync<PublicationDto>($"api/admin/publications/{Id}/restore");
        TempData["SuccessMessage"] = "مطلب بازگردانی شد و به صورت پیش‌نویس است؛ برای نمایش در پورتال آن را منتشر کنید.";
        return RedirectToPage("Edit", new { id = Id });
    }
    private async Task LoadImageVersionAsync()
    {
        if (!Id.HasValue) return;
        var current = await api.GetAsync<PublicationDto>($"api/admin/publications/{Id}");
        ImageVersion = current.ImageVersion; IsArchived = current.IsArchived;
    }
}
