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
public sealed class PublicController(ApplicationCatalogService applications, PublicationService publications) : ControllerBase
{
    [HttpGet("applications")]
    public Task<List<PublicApplicationDto>> Applications() => applications.PublicAsync();

    [HttpGet("publications")]
    public Task<PublicationListDto> Publications(string? kind = null, string? search = null, int page = 1) => publications.ListPublicAsync(kind, search, page);

    [HttpGet("publications/{id:long}")]
    public Task<PublicationDto> Publication(long id) => publications.GetPublicAsync(id);

    /// <summary>تصویر شاخص فقط برای مطلب منتشرشده‌ی عمومی؛ برای پیش‌نویس یا مطلب داخلی «یافت نشد».</summary>
    [HttpGet("publications/{id:long}/image"), EnableRateLimiting("public-media")]
    public async Task<IActionResult> PublicationImage(long id) => PublicationImages.File(this, await publications.GetImageAsync(id, OrganizationIntranet.Application.Abstractions.PublicationScope.Public));
}
