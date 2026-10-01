using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrganizationIntranet.Api.Security;
using OrganizationIntranet.Application.Services;
using OrganizationIntranet.Contracts;

namespace OrganizationIntranet.Api.Controllers;

[ApiController, Route("api/admin/catalog-import"), Authorize(Roles = "ADMIN"), AdminClient]
public sealed class CatalogImportController(OrganizationCatalogImportService service, ILogger<CatalogImportController> logger) : ControllerBase
{
    [HttpGet] public Task<CatalogImportPreviewDto> Preview() => service.PreviewAsync();
    [HttpPost] public async Task<CatalogImportResultDto> Import()
    {
        var result = await service.ImportAsync();
        logger.LogInformation("Organization application catalog imported by {UserId}; created {Created}, existing {Existing}", User.FindFirstValue(ClaimTypes.NameIdentifier), result.Created, result.Existing);
        return result;
    }
}
