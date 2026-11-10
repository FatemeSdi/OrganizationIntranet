using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Xunit;

namespace OrganizationIntranet.Tests;

/// <summary>
/// نشان شمار اعلان روی زنگ پورتال: روی گوشه‌ی دکمه می‌نشیند (نه روی خود زنگ)، برای عدد دورقمی و «99+» به‌اندازه‌ی کافی پهن است،
/// کلیک را نمی‌گیرد و شمار بزرگ در هر دو زنگ (هدر مشترک و صفحه‌ی اصلی) به «99+» کوتاه می‌شود.
/// </summary>
[Trait("Category", "Unit")]
public sealed class PortalBellBadgeTests
{
    private static string Root([CallerFilePath] string source = "") => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(source)!, "..", "..", ".."));
    private static string Read(params string[] parts) => File.ReadAllText(Path.Combine([Root(), .. parts]));

    private static string Rule(string css, string selector)
    {
        var match = Regex.Match(css, Regex.Escape(selector) + @"\s*\{(?<body>[^}]*)\}");
        Assert.True(match.Success, selector + " not found");
        return match.Groups["body"].Value;
    }

    [Theory]
    [InlineData("wwwroot/css/portal-shell.css", ".pp-icon-btn .pp-count")]
    [InlineData("src/OrganizationIntranet.Portal/Pages/Portal/Index.cshtml", ".badge-dot")]
    public void Badge_sits_on_the_button_corner_is_wide_enough_and_does_not_steal_clicks(string file, string selector)
    {
        var rule = Rule(Read(file.Split('/')), selector);
        Assert.Matches(@"top:\s*-\d+px", rule);                         // بیرون از گوشه، نه روی آیکن
        Assert.Matches(@"left:\s*-\d+px", rule);
        Assert.Matches(@"min-width:\s*(1[89]|2\d)px", rule);            // «99+» جا می‌شود
        Assert.Matches(@"box-sizing:\s*border-box", rule);
        Assert.Contains("pointer-events: none", rule.Replace("pointer-events:none", "pointer-events: none"));
    }

    [Fact]
    public void Large_unread_counts_are_capped_at_99_plus_in_both_bells()
    {
        Assert.Contains("> 99 ? \"99+\"", Read("src", "OrganizationIntranet.Portal", "Pages", "Shared", "Components", "PortalHeader", "Default.cshtml"));
        Assert.Contains("> 99 ? \"99+\"", Read("src", "OrganizationIntranet.Portal", "Pages", "Portal", "Index.cshtml"));
    }
}
