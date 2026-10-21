using Microsoft.AspNetCore.Mvc;

namespace OrganizationIntranet.UI;

/// <summary>پاسخ تصویر شاخص مطلب در UI؛ فقط نوع‌های تصویری مجاز عبور می‌کنند و مرورگر نوع را حدس نمی‌زند.</summary>
public static class PublicationImageResult
{
    private static readonly HashSet<string> Allowed = ["image/jpeg", "image/png", "image/webp"];
    public static async Task<IActionResult> FromApiAsync(ApiClient api, string path, HttpResponse response)
    {
        try
        {
            var file = await api.GetFileAsync(path);
            if (!Allowed.Contains(file.ContentType)) return new NotFoundResult();
            response.Headers.CacheControl = "private, max-age=86400";
            response.Headers.XContentTypeOptions = "nosniff";
            return new FileContentResult(file.Content, file.ContentType);
        }
        catch (ApiException ex) when (ex.Status is System.Net.HttpStatusCode.NotFound or System.Net.HttpStatusCode.BadRequest) { return new NotFoundResult(); }
    }
}
