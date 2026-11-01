using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrganizationIntranet.Api.Security;
using OrganizationIntranet.Application.Services;
using OrganizationIntranet.Contracts;

namespace OrganizationIntranet.Api.Controllers;

/// <summary>درخواست دسترسی کاربر به سامانه‌ها: ثبت، فهرست درخواست‌های خودش و لغو. همه‌ی عملیات فقط روی درخواست‌های همین کاربر است.</summary>
[ApiController, Route("api/portal/access-requests"), Authorize]
public sealed class AccessRequestsController(AccessRequestService service, ILogger<AccessRequestsController> logger) : ControllerBase
{
    /// <summary>شناسه‌ی کاربر واردشده از Claim توکن.</summary>
    private long UserId => long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    /// <summary>سامانه‌هایی که کاربر می‌تواند برایشان درخواست دسترسی ثبت کند (فعال، بدون دسترسی فعلی و بدون درخواست باز).</summary>
    [HttpGet("applications")] public Task<List<RequestableApplicationDto>> Applications() => service.RequestableAsync(UserId);
    /// <summary>فهرست درخواست‌های خود کاربر با فیلتر وضعیت و صفحه‌بندی سمت سرور.</summary>
    [HttpGet] public Task<AccessRequestListDto> List(string? status = null, int page = 1) => service.ListAsync(UserId, status, page);
    /// <summary>ثبت درخواست دسترسی تازه؛ برای هر سامانه همزمان فقط یک درخواست باز مجاز است و ناظران آن سامانه هشدار می‌گیرند.</summary>
    [HttpPost] public async Task<OperationResult> Create(CreateAccessRequest input)
    {
        await service.CreateAsync(UserId, input);
        logger.LogInformation("Access requested for application {ApplicationId} by {UserId}", input.ApplicationId, UserId);
        return new(true, "درخواست شما ثبت شد و در انتظار بررسی مدیر سامانه است.");
    }
    /// <summary>لغو درخواست در انتظار؛ با شماره‌ی نسخه (Revision) جلوی تعارض با تصمیم هم‌زمان مدیر را می‌گیرد.</summary>
    [HttpPost("{id:long}/cancel")] public async Task<OperationResult> Cancel(long id, CancelAccessRequest input)
    {
        await service.CancelAsync(UserId, id, input.Revision);
        logger.LogInformation("Access request {RequestId} cancelled by {UserId}", id, UserId);
        return new(true, "درخواست لغو شد.");
    }
}

/// <summary>بررسی درخواست‌های دسترسی در پنل مدیریت (نیاز به ACCESS_REQUEST_REVIEW): فهرست همه‌ی درخواست‌ها و تأیید یا رد.</summary>
[ApiController, Route("api/admin/access-requests"), Authorize(Policy = OrganizationIntranet.Contracts.AdminPermissions.IntranetAdminPolicy), Authorize(Policy = OrganizationIntranet.Contracts.AdminPermissions.AccessRequestReviewPolicy), AdminClient]
public sealed class AccessRequestAdminController(AccessRequestService service, ILogger<AccessRequestAdminController> logger) : ControllerBase
{
    /// <summary>فهرست همه‌ی درخواست‌ها (همه‌ی کاربران) با فیلتر وضعیت و صفحه‌بندی سمت سرور.</summary>
    [HttpGet] public Task<AccessRequestListDto> List(string? status = null, int page = 1) => service.ListAsync(null, status, page);
    /// <summary>تأیید یا رد درخواست در انتظار. با تأیید، دسترسی مستقیم کاربر به سامانه ساخته می‌شود (اگر قبلاً نداشته)؛ برای رد، نوشتن دلیل الزامی است.</summary>
    [HttpPost("{id:long}/review")] public async Task<OperationResult> Review(long id, ReviewAccessRequest input)
    {
        var actor = long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await service.ReviewAsync(actor, id, input);
        logger.LogInformation("Access request {RequestId} reviewed by {UserId}; approved: {Approved}", id, actor, input.Approve);
        return new(true, input.Approve ? "درخواست تأیید و دسترسی برقرار شد." : "درخواست رد شد.");
    }
}
