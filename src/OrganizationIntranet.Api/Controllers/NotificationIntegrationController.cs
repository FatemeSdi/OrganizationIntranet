using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using OrganizationIntranet.Api.Security;
using OrganizationIntranet.Application.Services;
using OrganizationIntranet.Contracts;

namespace OrganizationIntranet.Api.Controllers;

[ApiController, Route("api/integrations/applications/{applicationCode}/notifications"), ApplicationIntegration]
[Authorize(AuthenticationSchemes = ApplicationIntegrationHandler.SchemeName), EnableRateLimiting("integration")]
public sealed class NotificationIntegrationController(NotificationIntegrationService service, ILogger<NotificationIntegrationController> logger) : ControllerBase
{
    [HttpPost, RequestSizeLimit(262144)]
    public async Task<NotificationImportResult> Receive(ApplicationNotificationBatch batch)
    {
        var code = User.FindFirstValue("application_code")!;
        var result = await service.ImportAsync(code, batch);
        logger.LogInformation("Imported {Imported} source notifications for application {ApplicationCode}; unchanged {Ignored}", result.Imported, code, result.Ignored);
        return result;
    }
}
