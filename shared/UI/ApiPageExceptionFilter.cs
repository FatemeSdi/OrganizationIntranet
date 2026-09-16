using System.Net;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace OrganizationIntranet.UI;

public sealed class ApiPageExceptionFilter : IAsyncExceptionFilter
{
    public async Task OnExceptionAsync(ExceptionContext context)
    {
        if (context.Exception is not ApiException error) return;
        context.ExceptionHandled = true;
        if (error.Status == HttpStatusCode.Unauthorized)
        {
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            context.Result = new RedirectToPageResult("/Account/Login");
        }
        else if (error.Status == HttpStatusCode.Forbidden) context.Result = new StatusCodeResult(403);
        else if (error.Status == HttpStatusCode.NotFound) context.Result = new NotFoundResult();
        else context.Result = new RedirectToPageResult("/Error");
    }
}
