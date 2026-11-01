using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrganizationIntranet.Application.Abstractions;
using OrganizationIntranet.Application.Services;
using OrganizationIntranet.Contracts;
using OrganizationIntranet.Domain.Entities;

namespace OrganizationIntranet.Api.Controllers;

/// <summary>مطالب منتشرشده (خبر، اطلاعیه، بخشنامه) برای کاربر واردشده؛ پیش‌نویس و بایگانی‌شده دیده نمی‌شود.</summary>
[ApiController, Route("api/publications"), Authorize]
public sealed class PublicationsController(PublicationService service) : ControllerBase
{
    /// <summary>فهرست مطالب منتشرشده با فیلتر نوع، جست‌وجو و صفحه‌بندی (۲۰ مورد در هر صفحه).</summary>
    [HttpGet] public Task<PublicationListDto> List(string? kind = null, string? search = null, int page = 1) => service.ListAsync(true, kind, search, page);
    /// <summary>متن کامل یک مطلب منتشرشده.</summary>
    [HttpGet("{id:long}")] public Task<PublicationDto> Get(long id) => service.GetAsync(id, true);
    /// <summary>تصویر شاخص مطلب منتشرشده برای کاربر واردشده.</summary>
    [HttpGet("{id:long}/image")] public async Task<IActionResult> Image(long id) => PublicationImages.File(this, await service.GetImageAsync(id, PublicationScope.Published));
}

/// <summary>ابزار مشترک پاسخ تصویر شاخص مطالب برای کنترلرهای کاربر، عمومی و مدیر.</summary>
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

/// <summary>مدیریت اطلاع‌رسانی سازمانی در پنل (نیاز به PUBLICATION_EDIT): فهرست با فیلتر وضعیت، ساخت و ویرایش، بایگانی و بازگردانی و تصویر شاخص.</summary>
[OrganizationIntranet.Api.Security.AdminClient]
[ApiController, Route("api/admin/publications"), Authorize(Policy = OrganizationIntranet.Contracts.AdminPermissions.IntranetAdminPolicy), Authorize(Policy = OrganizationIntranet.Contracts.AdminPermissions.PublicationEditPolicy)]
public sealed class PublicationAdminController(PublicationService service, ILogger<PublicationAdminController> logger) : ControllerBase
{
    /// <summary>شناسه‌ی مدیر انجام‌دهنده؛ برای ثبت در لاگ و ستون UpdatedBy.</summary>
    private long Actor => long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    /// <summary>فهرست مطالب برای مدیر؛ status یکی از published، draft، public یا (برای مطالب بایگانی‌شده) archived و پیش‌فرض همه‌ی بایگانی‌نشده‌هاست.</summary>
    [HttpGet] public Task<PublicationListDto> List(string? kind = null, string? search = null, int page = 1, string? status = null) => service.ListForAdminAsync(status, kind, search, page);
    /// <summary>یک مطلب با هر وضعیتی (پیش‌نویس و بایگانی‌شده هم) برای فرم ویرایش.</summary>
    [HttpGet("{id:long}")] public Task<PublicationDto> Get(long id) => service.GetAsync(id, false);
    /// <summary>ساخت مطلب تازه (خبر، اطلاعیه یا بخشنامه).</summary>
    [HttpPost] public Task<PublicationDto> Create(PublicationEditRequest request) => Save(null, request);
    /// <summary>ویرایش مطلب؛ Revision ارسالی باید با نسخه‌ی فعلی یکی باشد وگرنه تعارض (۴۰۹) می‌دهد. مطلب بایگانی‌شده ویرایش نمی‌شود.</summary>
    [HttpPost("{id:long}")] public Task<PublicationDto> Update(long id, PublicationEditRequest request) => Save(id, request);
    /// <summary>منطق مشترک ساخت و ویرایش به‌همراه ثبت در لاگ.</summary>
    private async Task<PublicationDto> Save(long? id, PublicationEditRequest request)
    {
        var result = await service.SaveAsync(id, request, Actor);
        logger.LogInformation("Publication {PublicationId} saved by {UserId}; published: {Published}", result.PublicationId, Actor, result.IsPublished);
        return result;
    }
    [HttpPost("{id:long}/archive")]
    public async Task<PublicationDto> Archive(long id)
    {
        var result = await service.ArchiveAsync(id, Actor);
        logger.LogInformation("Publication {PublicationId} archived by {UserId}", id, Actor);
        return result;
    }
    [HttpPost("{id:long}/restore")]
    public async Task<PublicationDto> Restore(long id)
    {
        var result = await service.RestoreAsync(id, Actor);
        logger.LogInformation("Publication {PublicationId} restored from the archive by {UserId}", id, Actor);
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
