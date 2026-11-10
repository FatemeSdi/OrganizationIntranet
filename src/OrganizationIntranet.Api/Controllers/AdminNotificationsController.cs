using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrganizationIntranet.Application.Services;
using OrganizationIntranet.Contracts;
using P = OrganizationIntranet.Contracts.AdminPermissions;

namespace OrganizationIntranet.Api.Controllers;

/// <summary>API ارسال و مدیریت اعلان‌های سازمانی در پنل مدیریت. فقط از Admin (با کلید مدیریتی)، برای دارندگان INTRANET_ADMIN و مجوز NOTIFICATION_SEND (زنده از بانک).</summary>
[OrganizationIntranet.Api.Security.AdminClient]
[ApiController, Route("api/admin/notifications"), Authorize(Policy = P.IntranetAdminPolicy), Authorize(Policy = P.NotificationSendPolicy)]
public sealed class AdminNotificationsController(AdminNotificationService service, ILogger<AdminNotificationsController> logger) : ControllerBase
{
    /// <summary>شناسه‌ی مدیر فرستنده؛ برای ثبت در اعلان و لاگ.</summary>
    private long Actor => long.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
    /// <summary>اعلان‌های ارسال‌شده از جدیدترین، صفحه‌بندی‌شده.</summary>
    [HttpGet] public Task<AdminNotificationListDto> List(int page = 1) => service.ListAsync(page);
    /// <summary>ارسال اعلان به یک کاربر، یک واحد (با زیرواحدها) یا همه؛ پیام شمار گیرندگان را می‌گوید.</summary>
    [HttpPost] public async Task<OperationResult> Send(AdminNotificationRequest request)
    {
        var count = await service.SendAsync(request, Actor);
        logger.LogInformation("Admin notification sent by {ActorId} to {Audience} ({Count} recipients)", Actor, request.AudienceType, count);
        return new(true, $"اعلان برای {count} نفر ارسال شد");
    }
    /// <summary>پس‌گرفتن اعلان (از فهرست همه‌ی گیرندگان محو می‌شود).</summary>
    [HttpPost("{id:long}/deactivate")] public async Task<OperationResult> Deactivate(long id)
    {
        await service.SetActiveAsync(id, false);
        logger.LogInformation("Admin notification {NotificationId} withdrawn by {ActorId}", id, Actor);
        return new(true, "اعلان پس گرفته شد");
    }
    /// <summary>بازگرداندن اعلان پس‌گرفته‌شده (با همان وضعیت خوانده‌شدن گیرندگان).</summary>
    [HttpPost("{id:long}/activate")] public async Task<OperationResult> Activate(long id)
    {
        await service.SetActiveAsync(id, true);
        logger.LogInformation("Admin notification {NotificationId} restored by {ActorId}", id, Actor);
        return new(true, "اعلان دوباره فعال شد");
    }
}
