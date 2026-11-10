using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Core.Infrastructure;
using Microsoft.AspNetCore.Mvc.Filters;

namespace OrganizationIntranet.UI;

/// <summary>خطای Antiforgery صفحه‌ی ورود را به پاسخ JSON «اعتبار فرم پایان یافته» تبدیل می‌کند تا صفحه تازه شود، نه خطای خام.</summary>
public sealed class LoginAntiforgeryResultFilter : IAlwaysRunResultFilter
{
    public void OnResultExecuting(ResultExecutingContext context)
    {
        if (context.Result is IAntiforgeryValidationFailedResult
            && context.HttpContext.Request.Path.Equals("/Account/Login", StringComparison.OrdinalIgnoreCase))
        {
            context.Result = new JsonResult(new
            {
                success = false,
                code = "login_form_expired",
                message = "اعتبار فرم ورود پایان یافته است؛ صفحه تازه‌سازی می‌شود. لطفاً دوباره وارد شوید."
            }) { StatusCode = StatusCodes.Status400BadRequest };
        }
    }

    public void OnResultExecuted(ResultExecutedContext context) { }
}
