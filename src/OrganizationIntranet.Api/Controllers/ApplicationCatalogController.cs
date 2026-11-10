using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrganizationIntranet.Application.Services;
using OrganizationIntranet.Contracts;

namespace OrganizationIntranet.Api.Controllers;

/// <summary>API پورتال برای کاربر واردشده: سامانه‌های در اختیار او، ورود یکپارچه (SSO) و مرکز اعلان‌ها.</summary>
[ApiController, Route("api/portal"), Authorize]
public sealed class ApplicationCatalogController(ApplicationCatalogService service, NotificationCenterService center, SsoService sso, ILogger<ApplicationCatalogController> logger) : ControllerBase
{
    /// <summary>شناسه‌ی کاربر واردشده از Claim توکن.</summary>
    private long UserId => long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    /// <summary>سامانه‌های فعالی که کاربر به آن‌ها دسترسی دارد (مستقیم، از راه نقش یا عمومی)؛ هر کارت می‌گوید SSO دارد یا نه.</summary>
    [HttpGet("applications")] public Task<List<PortalApplicationDto>> Applications() => service.AvailableAsync(UserId);
    /// <summary>کلیک روی کارت سامانه‌ی دارای SSO: بلیت یک‌بارمصرف ۶۰ ثانیه‌ای برای همان سامانه می‌سازد؛ دسترسی و فعال بودن همین لحظه بررسی می‌شود.</summary>
    [HttpPost("applications/{id:int}/sso")] public async Task<SsoStartDto> StartSso(int id)
    {
        var result = await sso.StartAsync(UserId, id);
        logger.LogInformation("SSO ticket issued for application {ApplicationId} to user {UserId}", id, UserId);
        return result;
    }
    /// <summary>تصویر کارت سامانه‌ای که همین لحظه در دسترس کاربر است؛ برای هر سامانه‌ی دیگر «یافت نشد».</summary>
    [HttpGet("applications/{id:int}/image")] public async Task<IActionResult> Image(int id) => ApplicationImages.File(this, await service.GetAvailableImageAsync(UserId, id));
    /// <summary>اعلان‌های سامانه‌ها برای کاربر (آخرین ۵۰ مورد؛ برای زنگ هدر).</summary>
    [HttpGet("notifications")] public Task<List<PortalNotificationDto>> Notifications() => service.NotificationsAsync(UserId);
    /// <summary>مرکز اعلان‌ها: همه‌ی اعلان‌ها (سامانه‌ها و هشدار درخواست) با صفحه‌بندی سمت سرور. فیلترها: kind=system|request، status=unread|read، days=1|7|30|90.</summary>
    [HttpGet("notifications/page")] public Task<NotificationCenterDto> NotificationPage(string? kind = null, string? status = null, int? days = null, int page = 1)
        => center.PageAsync(UserId, kind, status, days, page);
    /// <summary>باز کردن یک اعلان: آن را خوانده‌شده می‌کند و مقصد امن را برمی‌گرداند.</summary>
    [HttpPost("notifications/{id:long}/open")] public Task<NotificationDestinationDto> OpenNotification(long id) => service.OpenNotificationAsync(UserId, id);
    /// <summary>همه‌ی اعلان‌های کاربر را خوانده‌شده می‌کند.</summary>
    [HttpPost("notifications/read")] public async Task<OperationResult> ReadNotifications()
    { await service.MarkNotificationsReadAsync(UserId); return new(true); }
}

/// <summary>ابزار مشترک پاسخ تصویر کارت سامانه برای کنترلرهای کاربر، عمومی و مدیر.</summary>
internal static class ApplicationImages
{
    /// <summary>نشانی تصویر نسخه‌دار است (?v=)، پس کش خصوصی مرورگر امن است؛ nosniff جلوی تفسیر بایت‌ها به‌عنوان نوع دیگر را می‌گیرد.</summary>
    public static IActionResult File(ControllerBase controller, OrganizationIntranet.Domain.Entities.ApplicationImage image)
    {
        controller.Response.Headers.CacheControl = "private, max-age=86400";
        controller.Response.Headers.XContentTypeOptions = "nosniff";
        return controller.File(image.Data, image.ContentType);
    }
}
