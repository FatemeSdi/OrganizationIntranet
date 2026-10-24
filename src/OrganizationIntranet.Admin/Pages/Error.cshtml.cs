using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OrganizationIntranet.UI;

namespace OrganizationIntranet.Pages
{
    /// <summary>
    /// صفحه‌ی خطای ۴۰۳/۴۰۴/۵۰۰ پنل مدیریت (UiHost: UseExceptionHandler و UseStatusCodePagesWithReExecute و مسیر /Account/AccessDenied).
    /// به API وابسته نیست (layout ورود، بدون زنگ اعلان) تا وقتی API در دسترس نیست هم نمایش داده شود.
    /// </summary>
    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    [IgnoreAntiforgeryToken]
    public class ErrorModel : PageModel
    {
        public ErrorPageContent Content { get; private set; } = ErrorPageContent.For(500);
        public string? RequestId { get; private set; }

        public void OnGet(int? code) => Build(code);
        public void OnPost(int? code) => Build(code); // re-execute همان متد درخواست اصلی را نگه می‌دارد

        private void Build(int? code)
        {
            var status = ErrorPageContent.Resolve(code, HttpContext.Request.Path.StartsWithSegments("/Account/AccessDenied"),
                HttpContext.Features.Get<IExceptionHandlerPathFeature>() is not null, HttpContext.Response.StatusCode);
            HttpContext.Response.StatusCode = status;
            Content = ErrorPageContent.For(status);
            RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        }
    }
}
