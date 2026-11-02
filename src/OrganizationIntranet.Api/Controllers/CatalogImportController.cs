using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrganizationIntranet.Api.Security;
using OrganizationIntranet.Application.Services;
using OrganizationIntranet.Contracts;

namespace OrganizationIntranet.Api.Controllers;

/// <summary>وارد کردن فهرست سامانه‌های مرجع سازمان (OrganizationApplicationCatalog) به کاتالوگ؛ سامانه‌ی موجود دست نمی‌خورد.</summary>
[ApiController, Route("api/admin/catalog-import"), Authorize(Policy = OrganizationIntranet.Contracts.AdminPermissions.IntranetAdminPolicy), AdminClient]
public sealed class CatalogImportController(OrganizationCatalogImportService service, ILogger<CatalogImportController> logger) : ControllerBase
{
    /// <summary>پیش‌نمایش ورود فهرست مرجع: هر سامانه و تخصیص اولیه‌اش و اینکه از قبل وجود دارد یا نه (بدون تغییر در بانک)؛ حساب fatemeh.sadeghi و نقش EXPERT باید فعال باشند.</summary>
    [HttpGet, Authorize(Policy = OrganizationIntranet.Contracts.AdminPermissions.ApplicationViewPolicy)] public Task<CatalogImportPreviewDto> Preview() => service.PreviewAsync();
    /// <summary>ثبت سامانه‌های مرجعِ ناموجود در کاتالوگ در یک ذخیره: GATEWAY عمومی می‌شود، PAYSLIP برای کاربر fatemeh.sadeghi و ATTENDANCE برای نقش EXPERT دسترسی می‌گیرد. نتیجه شمار ساخته‌شده و موجود را می‌دهد.</summary>
    [HttpPost, Authorize(Policy = OrganizationIntranet.Contracts.AdminPermissions.ApplicationEditPolicy)] public async Task<CatalogImportResultDto> Import()
    {
        var result = await service.ImportAsync();
        logger.LogInformation("Organization application catalog imported by {UserId}; created {Created}, existing {Existing}", User.FindFirstValue(ClaimTypes.NameIdentifier), result.Created, result.Existing);
        return result;
    }
}
