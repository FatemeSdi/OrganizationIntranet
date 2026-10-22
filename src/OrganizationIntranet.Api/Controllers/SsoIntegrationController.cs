using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using OrganizationIntranet.Api.Security;
using OrganizationIntranet.Application.Services;
using OrganizationIntranet.Contracts;

namespace OrganizationIntranet.Api.Controllers;

/// <summary>سرور سامانه‌ی مقصد بلیت را با کلید SSO خودش (هدر X-Application-Key) تحویل می‌گیرد؛ کلید داخلی UI لازم نیست.</summary>
[ApiController, Route("api/integrations/applications/{applicationCode}/sso"), ApplicationIntegration(KeyName = "SsoKey")]
[Authorize(AuthenticationSchemes = ApplicationIntegrationHandler.SchemeName), EnableRateLimiting("integration")]
public sealed class SsoIntegrationController(SsoService service, ILogger<SsoIntegrationController> logger) : ControllerBase
{
    [HttpPost("redeem"), RequestSizeLimit(4096)]
    public async Task<SsoIdentityDto> Redeem(SsoRedeemRequest request)
    {
        var code = User.FindFirstValue("application_code")!;
        try
        {
            var identity = await service.RedeemAsync(code, request.Ticket);
            logger.LogInformation("SSO ticket redeemed by application {ApplicationCode} for user {Username}", code, identity.Username);
            return identity;
        }
        catch (ArgumentException)
        {
            logger.LogWarning("SSO ticket rejected for application {ApplicationCode}", code);
            throw;
        }
    }
}
