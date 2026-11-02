using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Xunit;

namespace OrganizationIntranet.Tests;

/// <summary>
/// اسکریپت یک‌بارِ وارد کردن چارت سازمانی از ITS (tools/Import-OrganizationChartFromIts.ps1) هرگز چیزی در ITS نمی‌نویسد،
/// به‌طور پیش‌فرض آزمایشی است و رشته‌ی اتصال یا رمزی چاپ نمی‌کند. خود اسکریپت هیچ‌وقت در آزمون اجرا نمی‌شود.
/// </summary>
[Trait("Category", "Unit")]
public sealed class OrganizationImportScriptTests
{
    private static string Script([CallerFilePath] string source = "") =>
        File.ReadAllText(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(source)!, "..", "..", "..", "tools", "Import-OrganizationChartFromIts.ps1")));

    [Fact]
    public void Source_database_is_only_read_with_a_single_select()
    {
        var script = Script();
        Assert.Matches(@"Read-Rows\s+\$SourceConnectionString\s+""SELECT\s", script);
        // The only function that touches the source connection is Read-Rows; everything else uses the destination connection.
        Assert.Equal(1, Regex.Matches(script, @"\$SourceConnectionString").Count(m => script[Math.Max(0, m.Index - 12)..m.Index].Contains("Rows ")));
        Assert.DoesNotMatch(@"(?i)\b(INSERT|UPDATE|DELETE|DROP|ALTER|TRUNCATE)\b[^\r\n]*\$SourceTable", script);
    }

    [Fact]
    public void Import_is_a_dry_run_unless_apply_is_given_and_is_repeatable_by_source_id()
    {
        var script = Script();
        Assert.Contains("[switch]$Apply", script);
        Assert.Contains("if ($Apply) { $transaction.Commit()", script);
        Assert.Contains("$transaction.Rollback()", script);
        Assert.Contains("WHERE SourceId=@s", script);                                  // واحد موجود دوباره ساخته نمی‌شود
        Assert.Contains("012_OrganizationChart", script);                              // بدون migration اجرا نمی‌شود
    }

    [Fact]
    public void Import_never_prints_connection_strings_or_passwords()
    {
        var script = Script();
        Assert.DoesNotMatch(@"(?i)Write-(Output|Host)[^\r\n]*\$(destination|SourceConnectionString|connectionString)\b", script);
        Assert.DoesNotMatch(@"(?i)(password|pwd)\s*=\s*[^\s;""$]+", script.Replace("Password", ""));   // هیچ رمز ثابتی در اسکریپت نیست
        Assert.Contains("No connection details were logged", script);
    }
}
