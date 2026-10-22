using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrganizationIntranet.Api.Security;
using OrganizationIntranet.Application.Services;
using OrganizationIntranet.Contracts;

namespace OrganizationIntranet.Api.Controllers;

[ApiController, Route("api/admin/catalog-import"), Authorize(Policy = OrganizationIntranet.Contracts.AdminPermissions.IntranetAdminPolicy), AdminClient]
public sealed class CatalogImportController(OrganizationCatalogImportService service, ILogger<CatalogImportController> logger) : ControllerBase
{
    [HttpGet, Authorize(Policy = OrganizationIntranet.Contracts.AdminPermissions.ApplicationViewPolicy)] public Task<CatalogImportPreviewDto> Preview() => service.PreviewAsync();
    [HttpPost, Authorize(Policy = OrganizationIntranet.Contracts.AdminPermissions.ApplicationEditPolicy)] public async Task<CatalogImportResultDto> Import()
    {
        var result = await service.ImportAsync();
        logger.LogInformation("Organization application catalog imported by {UserId}; created {Created}, existing {Existing}", User.FindFirstValue(ClaimTypes.NameIdentifier), result.Created, result.Existing);
        return result;
    }
}
