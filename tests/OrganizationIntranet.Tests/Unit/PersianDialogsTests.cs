using Xunit;

namespace OrganizationIntranet.Tests;

// هیچ پیام یا هشداری برای کاربر نباید انگلیسی باشد. هشدار «تغییرات ذخیره‌نشده» نباید از beforeunload مرورگر بیاید (متنش انگلیسی است و صفحه نمی‌تواند آن را عوض کند).
public sealed class PersianDialogsTests
{
    private static readonly string Wwwroot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "wwwroot", "assets", "js"));

    [Fact]
    public void Dirty_form_warning_is_a_persian_dialog_and_never_the_browsers_english_beforeunload_prompt()
    {
        var js = File.ReadAllText(Path.Combine(Wwwroot, "admin-ui.js"));
        Assert.DoesNotContain("addEventListener('beforeunload'", js);
        Assert.DoesNotContain("returnValue", js);
        Assert.Contains("تغییرات ذخیره‌نشده", js);
        Assert.Contains("ماندن در صفحه", js);
        Assert.Contains("خروج بدون ذخیره", js);
        Assert.Contains("popstate", js);                  // دکمه‌ی برگشت مرورگر هم با همین پنجره‌ی فارسی گرفته می‌شود
    }

    [Fact]
    public void No_portal_or_admin_page_registers_a_beforeunload_prompt()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var offenders = new[] { Path.Combine(root, "src"), Path.Combine(root, "wwwroot", "assets", "js") }
            .SelectMany(d => Directory.EnumerateFiles(d, "*.*", SearchOption.AllDirectories))
            .Where(f => (f.EndsWith(".cshtml") || f.EndsWith(".js")) && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") && !f.EndsWith(".min.js"))
            .Where(f => File.ReadAllText(f).Contains("beforeunload", StringComparison.Ordinal) && !f.EndsWith("admin-ui.js"))
            .ToList();
        Assert.Empty(offenders);
    }
}
