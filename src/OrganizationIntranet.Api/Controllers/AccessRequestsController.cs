using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrganizationIntranet.Api.Security;
using OrganizationIntranet.Application.Services;
using OrganizationIntranet.Contracts;

namespace OrganizationIntranet.Api.Controllers;

[ApiController, Route("api/portal/access-requests"), Authorize]
public sealed class AccessRequestsController(AccessRequestService service, ILogger<AccessRequestsController> logger) : ControllerBase
{
    private long UserId => long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    [HttpGet("applications")] public Task<List<RequestableApplicationDto>> Applications() => service.RequestableAsync(UserId);
    [HttpGet] public Task<AccessRequestListDto> List(string? status = null, int page = 1) => service.ListAsync(UserId, status, page);
    [HttpPost] public async Task<OperationResult> Create(CreateAccessRequest input)
    {
        await service.CreateAsync(UserId, input);
        logger.LogInformation("Access requested for application {ApplicationId} by {UserId}", input.ApplicationId, UserId);
        return new(true, "درخواست شما ثبت شد و در انتظار بررسی مدیر سامانه است.");
    }
    [HttpPost("{id:long}/cancel")] public async Task<OperationResult> Cancel(long id, CancelAccessRequest input)
    {
        await service.CancelAsync(UserId, id, input.Revision);
        logger.LogInformation("Access request {RequestId} cancelled by {UserId}", id, UserId);
        return new(true, "درخواست لغو شد.");
    }
}

[ApiController, Route("api/admin/access-requests"), Authorize(Roles = "ADMIN"), AdminClient]
public sealed class AccessRequestAdminController(AccessRequestService service, ILogger<AccessRequestAdminController> logger) : ControllerBase
{
    [HttpGet] public Task<AccessRequestListDto> List(string? status = null, int page = 1) => service.ListAsync(null, status, page);
    [HttpPost("{id:long}/review")] public async Task<OperationResult> Review(long id, ReviewAccessRequest input)
    {
        var actor = long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await service.ReviewAsync(actor, id, input);
        logger.LogInformation("Access request {RequestId} reviewed by {UserId}; approved: {Approved}", id, actor, input.Approve);
        return new(true, input.Approve ? "درخواست تأیید و دسترسی برقرار شد." : "درخواست رد شد.");
    }
}
