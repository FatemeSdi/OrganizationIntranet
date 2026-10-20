using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrganizationIntranet.Application.Services;
using OrganizationIntranet.Contracts;

namespace OrganizationIntranet.Api.Controllers;

[ApiController, Route("api/portal"), Authorize]
public sealed class ApplicationCatalogController(ApplicationCatalogService service) : ControllerBase
{
    private long UserId => long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    [HttpGet("applications")] public Task<List<PortalApplicationDto>> Applications() => service.AvailableAsync(UserId);
    [HttpGet("notifications")] public Task<List<PortalNotificationDto>> Notifications() => service.NotificationsAsync(UserId);
    [HttpPost("notifications/{id:long}/open")] public Task<NotificationDestinationDto> OpenNotification(long id) => service.OpenNotificationAsync(UserId, id);
    [HttpPost("notifications/read")] public async Task<OperationResult> ReadNotifications()
    { await service.MarkNotificationsReadAsync(UserId); return new(true); }
}
