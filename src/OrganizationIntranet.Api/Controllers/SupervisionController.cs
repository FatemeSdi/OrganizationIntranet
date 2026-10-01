using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrganizationIntranet.Api.Security;
using OrganizationIntranet.Application.Services;
using OrganizationIntranet.Contracts;

namespace OrganizationIntranet.Api.Controllers;

[ApiController, Route("api/portal/supervision"), Authorize]
public sealed class SupervisionController(SupervisionService service) : ControllerBase
{
    private long UserId => long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    [HttpGet("inbox")] public Task<AccessRequestListDto> Inbox(int page = 1) => service.InboxAsync(UserId, page);
    [HttpGet("alerts")] public Task<AccessRequestAlertsDto> Alerts() => service.AlertsAsync(UserId);
    [HttpPost("alerts/read")] public async Task<OperationResult> ReadAll() { await service.MarkReadAsync(UserId, null); return new(true); }
    [HttpPost("alerts/{id:long}/read")] public async Task<OperationResult> Read(long id) { await service.MarkReadAsync(UserId, id); return new(true); }
}

[ApiController, Route("api/admin/supervision"), Authorize(Roles = "ADMIN"), AdminClient]
public sealed class SupervisionAdminController(SupervisionService service, ILogger<SupervisionAdminController> logger) : ControllerBase
{
    private long UserId => long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    [HttpGet("applications/{id:int}/groups")] public Task<List<SupervisionGroupDto>> Groups(int id) => service.GroupsAsync(id);
    [HttpPost("applications/{id:int}/groups")] public async Task<OperationResult> SaveGroup(int id, SupervisionGroupRequest input)
    { await service.SaveGroupAsync(id, input); logger.LogInformation("Supervision group {GroupId} saved for application {ApplicationId} by {UserId}", input.Id, id, UserId); return new(true, "گروه نظارت ذخیره شد."); }
    [HttpGet("alerts")] public Task<AccessRequestAlertsDto> Alerts() => service.AlertsAsync(UserId);
    [HttpPost("alerts/{id:long}/read")] public async Task<OperationResult> Read(long id) { await service.MarkReadAsync(UserId, id); return new(true); }
    [HttpGet("sms")] public Task<AccessRequestSmsListDto> Sms(int page = 1) => service.SmsAsync(page);
    [HttpPost("sms/{id:long}/retry")] public async Task<OperationResult> Retry(long id)
    { await service.RetrySmsAsync(id); logger.LogInformation("Access request SMS {AlertId} requeued by {UserId}", id, UserId); return new(true, "پیامک برای تلاش مجدد در صف قرار گرفت."); }
}
