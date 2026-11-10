using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Xunit;

namespace OrganizationIntranet.Tests;

/// <summary>
/// هر نوع (class، record، interface، enum و struct) در کد برنامه (src و shared) باید یک <c>/// &lt;summary&gt;</c> فارسی داشته باشد.
/// اگر نوعی بدون توضیح اضافه شود این آزمون فهرست دقیق آن را گزارش می‌دهد؛ توضیح را بالای نوع (بالای attributeها) بنویسید.
/// </summary>
[Trait("Category", "Unit")]
public sealed class PersianSummaryTests
{
    private static string Root([CallerFilePath] string source = "") => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(source)!, "..", "..", ".."));

    private static readonly Regex TypeDeclaration = new(@"^\s*(public|internal)\s+((sealed|static|abstract|partial|readonly|unsafe)\s+)*(class|record\s+struct|record|struct|interface|enum)\s+(?<name>\w+)", RegexOptions.Compiled);
    private static readonly Regex Persian = new(@"[؀-ۿ]", RegexOptions.Compiled);

    private static IEnumerable<string> SourceFiles() => new[] { "src", "shared" }
        .SelectMany(folder => Directory.EnumerateFiles(Path.Combine(Root(), folder), "*.cs", SearchOption.AllDirectories))
        .Where(f => !Regex.IsMatch(f, @"[\\/](obj|bin)[\\/]") && !f.EndsWith(".g.cs") && !f.EndsWith(".Designer.cs") && !f.Contains("GlobalUsings"));

    /// <summary>نوع‌های بدون summary فارسی، به‌شکل «مسیر:خط NameOfType».</summary>
    private static List<string> Missing()
    {
        var missing = new List<string>();
        foreach (var file in SourceFiles())
        {
            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                var match = TypeDeclaration.Match(lines[i]);
                if (!match.Success || lines[i].TrimStart().StartsWith("//")) continue;
                var j = i - 1;
                while (j >= 0 && Regex.IsMatch(lines[j], @"^\s*\[")) j--;                           // از بالای attributeها
                var documented = false;
                for (var k = j; k >= 0 && lines[k].TrimStart().StartsWith("///"); k--)
                    if (lines[k].Contains("<summary>") || (Persian.IsMatch(lines[k]) && lines[k].TrimStart().StartsWith("///"))) { documented = true; break; }
                var summary = string.Join(" ", Enumerable.Range(0, Math.Max(0, j + 1)).Reverse().TakeWhile(k => lines[k].TrimStart().StartsWith("///")).Select(k => lines[k]));
                if (!documented || !Persian.IsMatch(summary)) missing.Add($"{Path.GetRelativePath(Root(), file)}:{i + 1} {match.Groups["name"].Value}");
            }
        }
        return missing;
    }

    [Fact]
    public void Every_type_in_src_and_shared_has_a_persian_summary()
    {
        var missing = Missing();
        Assert.True(missing.Count == 0, $"{missing.Count} type(s) without a Persian <summary>:{Environment.NewLine}" + string.Join(Environment.NewLine, missing.Take(60)));
    }

    [Fact]
    public void The_check_itself_finds_the_types_it_is_meant_to_guard()
    {
        // محافظ: اگر الگو یا مسیر خراب شود و هیچ نوعی دیده نشود، آزمون بالا بی‌صدا سبز نمی‌ماند.
        var declared = SourceFiles().Sum(f => File.ReadLines(f).Count(l => TypeDeclaration.IsMatch(l)));
        Assert.True(declared > 300, $"Only {declared} type declarations were found; the scan path or pattern is wrong.");
    }
}
