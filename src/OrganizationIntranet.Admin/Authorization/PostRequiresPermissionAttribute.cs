using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using OrganizationIntranet.Authorization;

namespace OrganizationIntranet.Admin.Authorization;

/// <summary>
/// صفحه با مجوز «مشاهده» باز می‌شود ولی هر درخواست تغییر (POST) مجوز «ویرایش» را می‌خواهد؛ Razor Pages روی تک handler مجوز نمی‌پذیرد.
/// این فقط لایه‌ی UI است؛ API همان مجوز را زنده از بانک دوباره بررسی می‌کند.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class PostRequiresPermissionAttribute(string permission) : Attribute, IAsyncPageFilter
{
    public string Permission { get; } = permission;
    public Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context) => Task.CompletedTask;
    public async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        var method = context.HttpContext.Request.Method;
        if (!HttpMethods.IsGet(method) && !HttpMethods.IsHead(method) && !context.HttpContext.User.HasPermission(Permission))
        {
            context.Result = new ForbidResult();
            return;
        }
        await next();
    }
}
