using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using OrganizationIntranet.Application.Services;
using OrganizationIntranet.Contracts;

namespace OrganizationIntranet.Api.Controllers;

/// <summary>
/// داده‌ی عمومی پرتال برای بازدیدکننده‌ی ناشناس (بدون Bearer). کلید داخلی کلاینت (X-Intranet-Client) همچنان لازم است؛
/// فقط سرور Portal آن را دارد. هیچ داده‌ی کاربر، اعلان یا سامانه‌ی خصوصی از این مسیرها برنمی‌گردد.
/// </summary>
[ApiController, Route("api/public"), AllowAnonymous, EnableRateLimiting("public")]
public sealed class PublicController(ApplicationCatalogService applications, PublicationService publications, LibraryService library) : ControllerBase
{
    /// <summary>سامانه‌های فعال و عمومی برای بازدیدکننده‌ی ناشناس.</summary>
    [HttpGet("applications")]
    public Task<List<PublicApplicationDto>> Applications() => applications.PublicAsync();

    /// <summary>اخبار و اطلاعیه‌های منتشرشده‌ی عمومی (با فیلتر نوع و جست‌وجو و صفحه‌بندی؛ slider=true فقط مطالب اسلایدر صفحه‌ی اصلی را به ترتیب اسلایدر می‌دهد).</summary>
    [HttpGet("publications")]
    public Task<PublicationListDto> Publications(string? kind = null, string? search = null, int page = 1, bool slider = false) => publications.ListPublicAsync(kind, search, page, slider);

    /// <summary>متن کامل یک مطلب منتشرشده‌ی عمومی؛ پیش‌نویس، داخلی یا بایگانی‌شده «یافت نشد» است.</summary>
    [HttpGet("publications/{id:long}")]
    public Task<PublicationDto> Publication(long id) => publications.GetPublicAsync(id);

    /// <summary>تصویر شاخص فقط برای مطلب منتشرشده‌ی عمومی؛ برای پیش‌نویس یا مطلب داخلی «یافت نشد».</summary>
    [HttpGet("publications/{id:long}/image"), EnableRateLimiting("public-media")]
    public async Task<IActionResult> PublicationImage(long id) => PublicationImages.File(this, await publications.GetImageAsync(id, OrganizationIntranet.Application.Abstractions.PublicationScope.Public));

    /// <summary>کتابخانه‌ی اسناد (قوانین، انتشارات، فرم‌ها): دسته‌های دارای سند فعال و (با documents=true) سندهای منطبق با بخش، دسته و جست‌وجو.</summary>
    [HttpGet("library")]
    public Task<LibraryCatalogDto> Library(string? group = null, int? category = null, string? q = null, bool documents = false) => library.CatalogAsync(group, category, q, documents);

    /// <summary>دانلود فایل یک سند فعال؛ همیشه attachment با نوع محتوای تعیین‌شده توسط سرور و nosniff. سند ناموجود یا غیرفعال یا بدون فایل «یافت نشد» است.</summary>
    [HttpGet("library/documents/{id:long}/file"), EnableRateLimiting("public-media")]
    public async Task<IActionResult> LibraryFile(long id)
    {
        var (document, contentType, content) = await library.OpenAsync(id);
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(content, contentType, document.FileName);
    }

    /// <summary>پیش‌نمایش درون‌خطی تصویر یک سند فعال (png یا jpg تا ۳ مگابایت)؛ برای هر سند دیگر یا غیرفعال «یافت نشد». با nosniff و CSP محدود.</summary>
    [HttpGet("library/documents/{id:long}/preview"), EnableRateLimiting("public-media")]
    public async Task<IActionResult> LibraryPreview(long id)
    {
        var (contentType, content) = await library.OpenPreviewAsync(id, forAdmin: false);
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers["Content-Security-Policy"] = "default-src 'none'; sandbox";
        Response.Headers.CacheControl = "public, max-age=300";
        return File(content, contentType);
    }
}
