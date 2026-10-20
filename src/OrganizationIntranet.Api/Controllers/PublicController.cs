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
}
