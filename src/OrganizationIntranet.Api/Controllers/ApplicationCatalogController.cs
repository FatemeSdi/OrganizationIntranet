using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrganizationIntranet.Application.Services;
using OrganizationIntranet.Contracts;

namespace OrganizationIntranet.Api.Controllers;

[ApiController, Route("api/portal"), Authorize]
public sealed class ApplicationCatalogController(ApplicationCatalogService service, NotificationCenterService center, SsoService sso, ILogger<ApplicationCatalogController> logger) : ControllerBase
{
    private long UserId => long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    [HttpGet("applications")] public Task<List<PortalApplicationDto>> Applications() => service.AvailableAsync(UserId);
    // کلیک روی کارت سامانه‌ی دارای SSO: بلیت یک‌بارمصرف برای همان سامانه (دسترسی و کد ملی همین لحظه بررسی می‌شوند).
    [HttpPost("applications/{id:int}/sso")] public async Task<SsoStartDto> StartSso(int id)
    {
        var result = await sso.StartAsync(UserId, id);
        logger.LogInformation("SSO ticket issued for application {ApplicationId} to user {UserId}", id, UserId);
        return result;
    }
    [HttpGet("notifications")] public Task<List<PortalNotificationDto>> Notifications() => service.NotificationsAsync(UserId);
    // همه‌ی اعلان‌ها (سامانه‌ها + هشدار درخواست) با صفحه‌بندی سمت سرور؛ kind=system|request، status=unread|read، days=1|7|30|90
    [HttpGet("notifications/page")] public Task<NotificationCenterDto> NotificationPage(string? kind = null, string? status = null, int? days = null, int page = 1)
        => center.PageAsync(UserId, kind, status, days, page);
    [HttpPost("notifications/{id:long}/open")] public Task<NotificationDestinationDto> OpenNotification(long id) => service.OpenNotificationAsync(UserId, id);
    [HttpPost("notifications/read")] public async Task<OperationResult> ReadNotifications()
    { await service.MarkNotificationsReadAsync(UserId); return new(true); }
}
