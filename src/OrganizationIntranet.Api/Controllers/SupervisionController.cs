using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrganizationIntranet.Api.Security;
using OrganizationIntranet.Application.Services;
using OrganizationIntranet.Contracts;

namespace OrganizationIntranet.Api.Controllers;

/// <summary>کارتابل نظارت برای ناظران سامانه‌ها (فقط‌خواندنی): درخواست‌های سامانه‌های تحت نظارت و هشدارهای آن‌ها. ناظر یعنی عضو یک گروه نظارت فعال، مستقیم یا از راه نقش.</summary>
[ApiController, Route("api/portal/supervision"), Authorize]
public sealed class SupervisionController(SupervisionService service) : ControllerBase
{
    /// <summary>شناسه‌ی کاربر واردشده از Claim توکن.</summary>
    private long UserId => long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    /// <summary>درخواست‌های سامانه‌های تحت نظارت ناظر، با فیلتر وضعیت و سامانه و صفحه‌بندی. سامانه‌ی غیرتحت‌نظارت = فهرست خالی.</summary>
    [HttpGet("inbox")] public Task<AccessRequestListDto> Inbox(int page = 1, string? status = null, int? applicationId = null) => service.InboxAsync(UserId, page, status, applicationId);
    /// <summary>خلاصه‌ی کارتابل: سامانه‌های تحت نظارت و شمار درخواست‌ها به تفکیک وضعیت (برای کارت صفحه‌ی اصلی).</summary>
    [HttpGet("inbox/summary")] public Task<SupervisionSummaryDto> InboxSummary(int? applicationId = null) => service.InboxSummaryAsync(UserId, applicationId);
    /// <summary>هشدارهای درخواست جدید این ناظر و شمار خوانده‌نشده‌ها.</summary>
    [HttpGet("alerts")] public Task<AccessRequestAlertsDto> Alerts() => service.AlertsAsync(UserId);
    /// <summary>همه‌ی هشدارهای این ناظر را خوانده‌شده می‌کند.</summary>
    [HttpPost("alerts/read")] public async Task<OperationResult> ReadAll() { await service.MarkReadAsync(UserId, null); return new(true); }
    /// <summary>یک هشدار را خوانده‌شده می‌کند.</summary>
    [HttpPost("alerts/{id:long}/read")] public async Task<OperationResult> Read(long id) { await service.MarkReadAsync(UserId, id); return new(true); }
}

/// <summary>مدیریت گروه‌های نظارت هر سامانه و صف پیامک درخواست‌ها در پنل مدیریت.</summary>
[ApiController, Route("api/admin/supervision"), Authorize(Policy = OrganizationIntranet.Contracts.AdminPermissions.IntranetAdminPolicy), AdminClient]
public sealed class SupervisionAdminController(SupervisionService service, ILogger<SupervisionAdminController> logger) : ControllerBase
{
    /// <summary>شناسه‌ی مدیر واردشده از Claim توکن.</summary>
    private long UserId => long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    /// <summary>گروه‌های نظارت یک سامانه با اعضایشان (نیاز به APPLICATION_VIEW).</summary>
    [HttpGet("applications/{id:int}/groups"), Authorize(Policy = OrganizationIntranet.Contracts.AdminPermissions.ApplicationViewPolicy)] public Task<List<SupervisionGroupDto>> Groups(int id) => service.GroupsAsync(id);
    /// <summary>ساخت یا ویرایش یک گروه نظارت (شناسه‌ی صفر = گروه جدید)؛ گروه فعال حداقل یک عضو می‌خواهد و Revision تعارض ویرایش هم‌زمان را می‌گیرد.</summary>
    [HttpPost("applications/{id:int}/groups"), Authorize(Policy = OrganizationIntranet.Contracts.AdminPermissions.ApplicationEditPolicy)] public async Task<OperationResult> SaveGroup(int id, SupervisionGroupRequest input)
    { await service.SaveGroupAsync(id, input); logger.LogInformation("Supervision group {GroupId} saved for application {ApplicationId} by {UserId}", input.Id, id, UserId); return new(true, "گروه نظارت ذخیره شد."); }
    /// <summary>هشدارهای مدیر (زنگ هدر پنل).</summary>
    [HttpGet("alerts")] public Task<AccessRequestAlertsDto> Alerts() => service.AlertsAsync(UserId);
    /// <summary>یک هشدار مدیر را خوانده‌شده می‌کند.</summary>
    [HttpPost("alerts/{id:long}/read")] public async Task<OperationResult> Read(long id) { await service.MarkReadAsync(UserId, id); return new(true); }
    /// <summary>صف و سابقه‌ی پیامک‌های اطلاع‌رسانی درخواست‌ها (نیاز به ACCESS_REQUEST_REVIEW).</summary>
    [HttpGet("sms"), Authorize(Policy = OrganizationIntranet.Contracts.AdminPermissions.AccessRequestReviewPolicy)] public Task<AccessRequestSmsListDto> Sms(int page = 1) => service.SmsAsync(page);
    /// <summary>پیامک ناموفق را دوباره در صف ارسال می‌گذارد (پس از اصلاح تنظیمات پیامک).</summary>
    [HttpPost("sms/{id:long}/retry"), Authorize(Policy = OrganizationIntranet.Contracts.AdminPermissions.AccessRequestReviewPolicy)] public async Task<OperationResult> Retry(long id)
    { await service.RetrySmsAsync(id); logger.LogInformation("Access request SMS {AlertId} requeued by {UserId}", id, UserId); return new(true, "پیامک برای تلاش مجدد در صف قرار گرفت."); }
}
