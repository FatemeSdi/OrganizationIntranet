using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OrganizationIntranet.UI;

namespace OrganizationIntranet.Pages.Library;

/// <summary>پیش‌نمایش درون‌خطی تصویر یک سند منتشرشده (png یا jpg تا ۳ مگابایت) برای بندانگشتی فهرست. API فقط همین تصویرها را می‌دهد؛ هر سند دیگر یا غیرفعال ۴۰۴ است. با nosniff و CSP محدود.</summary>
[AllowAnonymous]
public class PreviewModel(ApiClient api) : PageModel
{
    public async Task<IActionResult> OnGetAsync(long id)
    {
        try
        {
            var file = await api.GetFileAsync($"api/public/library/documents/{id}/preview");
            Response.Headers["X-Content-Type-Options"] = "nosniff";
            Response.Headers["Content-Security-Policy"] = "default-src 'none'; sandbox";
            Response.Headers.CacheControl = "public, max-age=300";
            return File(file.Content, file.ContentType);
        }
        catch (ApiException ex) when ((int)ex.Status == 404) { return NotFound(); }
    }
}
