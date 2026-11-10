using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OrganizationIntranet.UI;

namespace OrganizationIntranet.Pages.Library;

/// <summary>دانلود فایل یک سند منتشرشده برای همه (بدون ورود). Portal فایل را از API می‌گیرد و فقط به‌صورت attachment و با nosniff می‌دهد؛ سند ناموجود یا غیرفعال ۴۰۴ است.</summary>
[AllowAnonymous]
public class DownloadModel(ApiClient api) : PageModel
{
    public async Task<IActionResult> OnGetAsync(long id)
    {
        try
        {
            var file = await api.GetFileAsync($"api/public/library/documents/{id}/file");
            Response.Headers["X-Content-Type-Options"] = "nosniff";
            return File(file.Content, file.ContentType, file.FileName ?? "document");
        }
        catch (ApiException ex) when ((int)ex.Status == 404) { return NotFound(); }
    }
}
