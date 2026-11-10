extern alias PortalEntry;
extern alias AdminEntry;
using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace OrganizationIntranet.Tests;

// سرآیندهای امنیتی پایه‌ی هر دو UI: ضد قاب‌شدن (clickjacking)، nosniff، Referrer-Policy و Permissions-Policy، روی صفحه‌ی عمومی، خطا و فایل ایستا.
public sealed partial class PortalSmokeTests
{
    private static void AssertSecurityHeaders(HttpResponseMessage response, string path)
    {
        Assert.True(response.Headers.TryGetValues("X-Frame-Options", out var frame) && frame.Single() == "DENY", $"{path}: X-Frame-Options");
        Assert.True(response.Headers.TryGetValues("X-Content-Type-Options", out var sniff) && sniff.Single() == "nosniff", $"{path}: X-Content-Type-Options");
        Assert.Equal("strict-origin-when-cross-origin", response.Headers.GetValues("Referrer-Policy").Single());
        Assert.Contains("camera=()", response.Headers.GetValues("Permissions-Policy").Single());
    }

    [Fact]
    public async Task Both_UIs_send_the_basic_security_headers_on_pages_errors_and_static_files()
    {
        using var portal = Ui<PortalEntry::Program>("Portal");
        using var admin = Ui<AdminEntry::Program>("Admin");
        var options = new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false };
        foreach (var (ui, paths) in new[] { (portal.CreateClient(options), new[] { "/Portal", "/Account/Login", "/Library", "/no-such-page", "/assets/libs/bootstrap/css/bootstrap.rtl.min.css" }),
                                            (admin.CreateClient(options), new[] { "/Account/Login", "/Admin", "/no-such-page", "/assets/libs/bootstrap/css/bootstrap.rtl.min.css" }) })
            using (ui)
                foreach (var path in paths)
                {
                    var response = await ui.GetAsync(path);
                    Assert.NotEqual(HttpStatusCode.InternalServerError, response.StatusCode);
                    AssertSecurityHeaders(response, path);
                }
    }
}
