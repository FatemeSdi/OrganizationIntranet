using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using Xunit;

namespace OrganizationIntranet.Tests;

/// <summary>
/// فایل‌های database/migrations با فهرست ExpectedMigrations و README هم‌خوان‌اند، در git نادیده گرفته نمی‌شوند
/// و ابزار اجرا متن فارسی آن‌ها را درست (UTF-8) می‌خواند.
/// </summary>
[Trait("Category", "Unit")]
public sealed class MigrationFilesTests
{
    // 001 و 002 پیش از قاعده‌ی «up باید migration قبلی را بخواهد» نوشته شده‌اند.
    private static readonly HashSet<string> LegacyWithoutPrerequisite = ["001_DatabaseConsistency", "002_AuthenticationSchema"];

    private static string Root([CallerFilePath] string source = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(source)!, "..", "..", ".."));
    private static string MigrationsDir => Path.Combine(Root(), "database", "migrations");
    private static string[] UpFiles => Directory.GetFiles(MigrationsDir, "*.up.sql").OrderBy(f => f, StringComparer.Ordinal).ToArray();
    private static string IdOf(string upFile) => Path.GetFileName(upFile)[..^".up.sql".Length];

    [Fact]
    public void Every_migration_file_is_expected_and_listed_in_the_readme()
    {
        var expected = LiveMigrationStateTests.ExpectedMigrations;
        var readme = File.ReadAllText(Path.Combine(MigrationsDir, "README.md"));
        Assert.All(UpFiles.Select(IdOf), id => Assert.Contains(id, expected));
        Assert.All(expected, id => Assert.Contains($"`{id}`", readme));
    }

    [Fact]
    public void Every_up_script_records_itself_requires_its_predecessor_and_has_a_down_script()
    {
        var order = LiveMigrationStateTests.ExpectedMigrations;
        foreach (var file in UpFiles)
        {
            var id = IdOf(file);
            var sql = File.ReadAllText(file);
            Assert.Contains($"'{id}'", sql);
            Assert.True(File.Exists(Path.Combine(MigrationsDir, id + ".down.sql")), id + " has no down script");
            var index = Array.IndexOf(order, id);
            if (index > 0 && !LegacyWithoutPrerequisite.Contains(id))
                Assert.Contains($"MigrationId='{order[index - 1]}'", sql);
        }
    }

    [Fact]
    public void Fresh_install_baseline_records_only_the_migrations_it_contains_and_is_documented()
    {
        var baseline = File.ReadAllText(Path.Combine(MigrationsDir, "000_Baseline.install.sql"));
        Assert.Contains("('001_DatabaseConsistency'),('002_AuthenticationSchema')", baseline);
        Assert.DoesNotContain("003_ApplicationCatalog", baseline);                  // 003 به بعد همیشه با فایل خودشان اجرا می‌شوند
        Assert.Contains("THROW 51000,'The database is not empty.", baseline);
        Assert.DoesNotMatch(@"(?im)^\s*GO\s*$", baseline);
        Assert.Contains("000_Baseline.install.sql", File.ReadAllText(Path.Combine(MigrationsDir, "README.md")));
    }

    [Fact]
    public void Migration_files_are_valid_utf8_and_the_runner_reads_them_as_utf8()
    {
        var strict = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
        foreach (var file in Directory.GetFiles(MigrationsDir, "*.sql"))
            strict.GetString(File.ReadAllBytes(file)); // throws on non-UTF-8 bytes

        // Windows PowerShell 5.1 reads BOM-less files as ANSI; 008 once got garbled Persian names that way.
        var runner = File.ReadAllText(Path.Combine(Root(), "tools", "Invoke-DatabaseScript.ps1"));
        Assert.Matches(@"\$sql\s*=\s*Get-Content\s[^\r\n]*-Encoding\s+UTF8[^\r\n]*\$ScriptPath", runner);
    }

    [Fact]
    public void Migration_files_and_readme_are_not_ignored_by_git()
    {
        var files = Directory.GetFiles(MigrationsDir).Select(f => Path.GetRelativePath(Root(), f).Replace('\\', '/')).ToArray();
        var info = new ProcessStartInfo("git") { WorkingDirectory = Root(), RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in new[] { "check-ignore", "--no-index" }.Concat(files)) info.ArgumentList.Add(arg);
        Process? git;
        try { git = Process.Start(info); } catch (System.ComponentModel.Win32Exception) { return; } // git not installed: nothing to check
        var ignored = git!.StandardOutput.ReadToEnd();
        git.WaitForExit();
        if (git.ExitCode == 128) return; // not a git checkout (e.g. a source export)
        Assert.True(git.ExitCode == 1 && ignored.Trim().Length == 0, "Ignored by .gitignore: " + ignored);
    }
}
