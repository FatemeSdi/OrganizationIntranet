using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrganizationIntranet.Application.Abstractions;
using OrganizationIntranet.Application.Services;
using OrganizationIntranet.Contracts;
using OrganizationIntranet.Domain.Entities;

namespace OrganizationIntranet.Api.Controllers;

[ApiController, Route("api/publications"), Authorize]
public sealed class PublicationsController(PublicationService service) : ControllerBase
{
    [HttpGet] public Task<PublicationListDto> List(string? kind = null, string? search = null, int page = 1) => service.ListAsync(true, kind, search, page);
    [HttpGet("{id:long}")] public Task<PublicationDto> Get(long id) => service.GetAsync(id, true);
    /// <summary>تصویر شاخص مطلب منتشرشده برای کاربر واردشده.</summary>
    [HttpGet("{id:long}/image")] public async Task<IActionResult> Image(long id) => PublicationImages.File(this, await service.GetImageAsync(id, PublicationScope.Published));
}

internal static class PublicationImages
{
    /// <summary>نشانی تصویر نسخه‌دار است (?v=)، پس کش خصوصی مرورگر امن است؛ nosniff جلوی تفسیر بایت‌ها به‌عنوان نوع دیگر را می‌گیرد.</summary>
    public static IActionResult File(ControllerBase controller, PublicationImage image)
    {
        controller.Response.Headers.CacheControl = "private, max-age=86400";
        controller.Response.Headers.XContentTypeOptions = "nosniff";
        return controller.File(image.Data, image.ContentType);
    }
}

[OrganizationIntranet.Api.Security.AdminClient]
[ApiController, Route("api/admin/publications"), Authorize(Policy = OrganizationIntranet.Contracts.AdminPermissions.IntranetAdminPolicy), Authorize(Policy = OrganizationIntranet.Contracts.AdminPermissions.PublicationEditPolicy)]
public sealed class PublicationAdminController(PublicationService service, ILogger<PublicationAdminController> logger) : ControllerBase
{
    private long Actor => long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    [HttpGet] public Task<PublicationListDto> List(string? kind = null, string? search = null, int page = 1) => service.ListAsync(false, kind, search, page);
    [HttpGet("{id:long}")] public Task<PublicationDto> Get(long id) => service.GetAsync(id, false);
    [HttpPost] public Task<PublicationDto> Create(PublicationEditRequest request) => Save(null, request);
    [HttpPost("{id:long}")] public Task<PublicationDto> Update(long id, PublicationEditRequest request) => Save(id, request);
    private async Task<PublicationDto> Save(long? id, PublicationEditRequest request)
    {
        var result = await service.SaveAsync(id, request, Actor);
        logger.LogInformation("Publication {PublicationId} saved by {UserId}; published: {Published}", result.PublicationId, Actor, result.IsPublished);
        return result;
    }
    /// <summary>تصویر پیش‌نویس‌ها هم برای مدیر قابل مشاهده است.</summary>
    [HttpGet("{id:long}/image")] public async Task<IActionResult> Image(long id) => PublicationImages.File(this, await service.GetImageAsync(id, PublicationScope.All));
    // سقف بدنه: ۲ مگابایت تصویر به‌صورت base64 (حدود ۲٫۷ مگابایت) به‌اضافه‌ی حاشیه.
    [HttpPost("{id:long}/image"), RequestSizeLimit(3_200_000)]
    public async Task<PublicationDto> SetImage(long id, PublicationImageUpload request)
    {
        var result = await service.SetImageAsync(id, request.Base64, Actor);
        logger.LogInformation("Publication {PublicationId} image replaced by {UserId}", id, Actor);
        return result;
    }
    [HttpPost("{id:long}/image/remove")]
    public async Task<OperationResult> RemoveImage(long id)
    {
        await service.RemoveImageAsync(id, Actor);
        logger.LogInformation("Publication {PublicationId} image removed by {UserId}", id, Actor);
        return new(true, "تصویر حذف شد");
    }
}
