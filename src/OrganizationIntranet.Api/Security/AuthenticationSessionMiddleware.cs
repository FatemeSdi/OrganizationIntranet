using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using OrganizationIntranet.Application.Services;

namespace OrganizationIntranet.Api.Security;

public sealed class AuthenticationSessionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, AuthenticationService authentication)
    {
        if (context.User.Identity?.IsAuthenticated == true && context.GetEndpoint()?.Metadata.GetMetadata<IAllowAnonymous>() is null)
        {
            if (!long.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ||
                !await authentication.SessionValidAsync(id, context.User.FindFirstValue("security_stamp"), context.User.FindFirstValue("auth_revision")))
            {
                context.Response.StatusCode = 401;
                return;
            }
        }
        await next(context);
    }
}
