using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrganizationIntranet.Application.Services;
using OrganizationIntranet.Contracts;
using P = OrganizationIntranet.Contracts.AdminPermissions;

namespace OrganizationIntranet.Api.Controllers;

/// <summary>API چارت سازمانی در پنل مدیریت. فقط از Admin (با کلید مدیریتی) و برای دارندگان INTRANET_ADMIN؛ هر عملیات مجوز دقیق خودش را زنده از بانک می‌خواهد.</summary>
[OrganizationIntranet.Api.Security.AdminClient]
[ApiController, Route("api/admin/org"), Authorize(Policy = P.IntranetAdminPolicy)]
public sealed class OrganizationController(OrganizationService service, ILogger<OrganizationController> logger) : ControllerBase
{
    /// <summary>شناسه‌ی مدیر انجام‌دهنده‌ی عملیات؛ برای ثبت در لاگ و UpdatedBy.</summary>
    private long Actor => long.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
    /// <summary>درخت واحدها به ترتیب پیمایش. فهرست برای انتخاب واحد در فرم کاربر هم لازم است و فقط ورود به پنل را می‌خواهد.</summary>
    [HttpGet("units")] public Task<List<OrgUnitDto>> Units() => service.TreeAsync();
    /// <summary>ساخت واحد تازه (ORG_EDIT).</summary>
    [HttpPost("units"), Authorize(Policy = P.OrgEditPolicy)] public async Task<OperationResult> Create(OrgUnitRequest request)
    {
        var unit = await service.CreateAsync(request, Actor);
        logger.LogInformation("Org unit {UnitId} created by {ActorId}", unit.UnitId, Actor);
        return new(true, "واحد جدید ایجاد شد");
    }
    /// <summary>ویرایش یا جابه‌جایی واحد (ORG_EDIT).</summary>
    [HttpPost("units/{id:int}"), Authorize(Policy = P.OrgEditPolicy)] public async Task<OperationResult> Update(int id, OrgUnitRequest request)
    {
        await service.UpdateAsync(id, request, Actor);
        logger.LogInformation("Org unit {UnitId} updated by {ActorId}", id, Actor);
        return new(true, "واحد ذخیره شد");
    }
    /// <summary>فعال/غیرفعال کردن واحد و زیرمجموعه‌هایش (ORG_EDIT).</summary>
    [HttpPost("units/{id:int}/toggle"), Authorize(Policy = P.OrgEditPolicy)] public async Task<OperationResult> Toggle(int id)
    {
        await service.ToggleAsync(id, Actor);
        logger.LogInformation("Org unit {UnitId} toggled by {ActorId}", id, Actor);
        return new(true);
    }
    /// <summary>حذف واحد بدون زیرمجموعه و کاربر (ORG_EDIT).</summary>
    [HttpPost("units/{id:int}/delete"), Authorize(Policy = P.OrgEditPolicy)] public async Task<OperationResult> Delete(int id)
    {
        await service.DeleteAsync(id);
        logger.LogInformation("Org unit {UnitId} deleted by {ActorId}", id, Actor);
        return new(true, "واحد حذف شد");
    }
}
