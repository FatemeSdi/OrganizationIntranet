using System.Security.Cryptography;
using System.Text;
using OrganizationIntranet.Contracts;

namespace OrganizationIntranet.Api.Security;

public sealed class AdminClientMiddleware(RequestDelegate next, IConfiguration configuration, ILogger<AdminClientMiddleware> logger)
{
    private readonly byte[] expected = SHA256.HashData(Encoding.UTF8.GetBytes(configuration["Api:AdminClientKey"]!));
    public async Task InvokeAsync(HttpContext context, LivePermissions permissions)
    {
        if (context.GetEndpoint()?.Metadata.GetMetadata<AdminClientAttribute>() is not null)
        {
            // Administrative decisions respect the user's current permissions in the database, not only cached login claims.
            if (context.User.Identity?.IsAuthenticated != true || !(await permissions.ForAsync(context.User)).Contains(AdminPermissions.IntranetAdmin))
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
