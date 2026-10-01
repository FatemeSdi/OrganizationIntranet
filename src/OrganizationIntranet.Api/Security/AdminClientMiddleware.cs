using System.Security.Cryptography;
using System.Text;

namespace OrganizationIntranet.Api.Security;

public sealed class AdminClientMiddleware(RequestDelegate next, IConfiguration configuration, ILogger<AdminClientMiddleware> logger)
{
    private readonly byte[] expected = SHA256.HashData(Encoding.UTF8.GetBytes(configuration["Api:AdminClientKey"]!));
    public async Task InvokeAsync(HttpContext context, OrganizationIntranet.Application.Abstractions.IIntranetRepository identities)
    {
        if (context.GetEndpoint()?.Metadata.GetMetadata<AdminClientAttribute>() is not null)
        {
            // Administrative decisions must respect current role revocation, not only cached login claims.
            if (!long.TryParse(context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var actorId))
            { context.Response.StatusCode = StatusCodes.Status403Forbidden; return; }
            var actor = await identities.FindUserAsync(actorId);
            if (actor is not { IsActive: true } || !actor.UserRoles.Any(r => r.Role.IsActive && r.Role.RoleCode == "ADMIN"))
            { context.Response.StatusCode = StatusCodes.Status403Forbidden; return; }
            var supplied = context.Request.Headers["X-Intranet-Admin"].ToString();
            if (!CryptographicOperations.FixedTimeEquals(expected, SHA256.HashData(Encoding.UTF8.GetBytes(supplied))))
            {
                logger.LogWarning("Rejected administrative request without trusted Admin client. TraceId: {TraceId}", context.TraceIdentifier);
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }
        }
        await next(context);
    }
}
