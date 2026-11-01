using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using OrganizationIntranet.Application.Services;

namespace OrganizationIntranet.Api.Security;

/// <summary>هر درخواست احرازشده را با بانک می‌سنجد: کاربر فعال باشد و مهر امنیتی و نسخه‌ی تنظیمات ورود در توکن با مقدار فعلی یکی باشد، وگرنه ۴۰۱. بنابراین تغییر رمز، غیرفعال‌شدن کاربر و تغییر تنظیمات فوراً همه‌ی توکن‌های قبلی را باطل می‌کند. مسیرهای ناشناس و اتصال سامانه‌ها از این بررسی معاف‌اند.</summary>
public sealed class AuthenticationSessionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, AuthenticationService authentication)
    {
        if (context.User.Identity?.IsAuthenticated == true && context.GetEndpoint()?.Metadata.GetMetadata<IAllowAnonymous>() is null
            && context.GetEndpoint()?.Metadata.GetMetadata<ApplicationIntegrationAttribute>() is null)
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
