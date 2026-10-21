extern alias PortalEntry;
extern alias AdminEntry;
using Xunit;

namespace OrganizationIntranet.Tests;

/// <summary>خطاهای راه‌اندازی باید بگویند کدام Environment فعال است؛ User Secrets فقط در Development خوانده می‌شود و این پرتکرارترین دلیل «کلید پیدا نشد» است.</summary>
[Trait("Category", "Integration")]
public sealed class StartupConfigurationTests
{
    private const string Strong = "startup-test-only-key-000000000000000000";
    private static string[] Args(string ui, string environment, params string[] extra) =>
        new[] { $"--Ui:Name={ui}", $"--environment={environment}", "--Api:BaseUrl=https://localhost:7232", "--Sites:Admin=https://localhost:7231" }.Concat(extra).ToArray();

    [Fact]
    public void Outside_development_the_error_names_the_environment_but_reveals_no_machine_details()
    {
        var error = Assert.Throws<InvalidOperationException>(() => AdminEntry::OrganizationIntranet.UI.UiHost.Create(Args("Admin", "Production", $"--Api:ClientKey={Strong}"), "OrganizationIntranet.Admin"));
        Assert.Contains("Api:AdminClientKey", error.Message);
        Assert.Contains("Environment: Production", error.Message);
        Assert.Contains("ASPNETCORE_ENVIRONMENT=Development", error.Message);
        Assert.DoesNotContain(Strong, error.Message); // مقدار کلید هرگز در پیام نمی‌آید
        // روی سرور مسیر فایل، نام کاربر ویندوز و نام کلیدها گفته نمی‌شود.
        Assert.DoesNotContain("windowsUser", error.Message);
        Assert.DoesNotContain("UserSecrets", error.Message);
    }

    [Fact]
    public void On_a_developer_machine_the_error_explains_where_it_looked_without_values()
    {
        // خط فرمان بر appsettings.Development.json و User Secrets مقدم است؛ کلید مدیریتی را عمداً خالی می‌کنیم.
        var error = Assert.Throws<InvalidOperationException>(() => AdminEntry::OrganizationIntranet.UI.UiHost.Create(
            // staticWebAssets: در Development، manifest دارایی‌های ایستا به wwwroot داخل پروژه اشاره می‌کند که در این مخزن مشترک و در ریشه است؛
            // در آزمون آن را به فایلی ناموجود می‌دهیم (ASP.NET یعنی «بدون manifest»). روی اجرای واقعی برنامه اثری ندارد.
            Args("Admin", "Development", $"--Api:ClientKey={Strong}", "--Api:AdminClientKey=", "--staticWebAssets=no-static-web-assets-manifest.json"), "OrganizationIntranet.Admin"));
        Assert.Contains("Environment: Development", error.Message);
        Assert.Contains($"windowsUser={Environment.UserName}", error.Message);
        Assert.Contains("id=OrganizationIntranet-Admin", error.Message);
        Assert.Contains("exists=", error.Message);
        Assert.Contains($"effective ClientKey len={Strong.Length}", error.Message);
        Assert.Contains("AdminClientKey len=0", error.Message);
        Assert.DoesNotContain(Strong, error.Message);
    }

    [Fact]
    public void Admin_rejects_an_admin_key_equal_to_the_client_key_and_a_short_client_key()
    {
        Assert.Contains("distinct", Assert.Throws<InvalidOperationException>(() => AdminEntry::OrganizationIntranet.UI.UiHost.Create(
            Args("Admin", "Staging", $"--Api:ClientKey={Strong}", $"--Api:AdminClientKey={Strong}"), "OrganizationIntranet.Admin")).Message);
        Assert.Contains("Api:ClientKey", Assert.Throws<InvalidOperationException>(() => PortalEntry::OrganizationIntranet.UI.UiHost.Create(
            Args("Portal", "Production", "--Api:ClientKey=short"), "OrganizationIntranet.Portal")).Message);
    }
}
