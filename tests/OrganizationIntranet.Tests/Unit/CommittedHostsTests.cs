using System.Runtime.CompilerServices;
using System.Text.Json;
using Xunit;

namespace OrganizationIntranet.Tests;

/// <summary>
/// آدرس درگاه پیامک داخلی مخصوص هر محیط است و در appsettings.json مخزن نمی‌ماند؛ در appsettings.Development.json (در git نیست)
/// یا تنظیمات استقرار می‌آید. نمونه‌اش appsettings.Development.example.json است. رشته‌ی اتصال بانک عمداً در appsettings.json می‌ماند (تصمیم صریح).
/// </summary>
public class CommittedHostsTests
{
    private static string Root([CallerFilePath] string source = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(source)!, "..", "..", ".."));
    private static string Api(string file) => Path.Combine(Root(), "src", "OrganizationIntranet.Api", file);
    private static JsonElement Load(string path) =>
        JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }).RootElement;

    [Fact]
    public void Committed_api_settings_hold_no_sms_gateway_address_but_keep_the_connection_string()
    {
        var settings = Load(Api("appsettings.json"));
        Assert.Equal("", settings.GetProperty("Sms").GetProperty("BaseUrl").GetString());
        Assert.False(string.IsNullOrWhiteSpace(settings.GetProperty("ConnectionStrings").GetProperty("DefaultConnection").GetString()));
        Assert.DoesNotMatch(@"\b(10|192\.168|172\.(1[6-9]|2\d|3[01]))\.\d+\.\d+(\.\d+)?\b", File.ReadAllText(Api("appsettings.json")));   // IP شبکه‌ی داخلی
    }

    [Fact]
    public void Development_example_names_the_sms_address_and_the_real_file_stays_ignored()
    {
        Assert.True(Load(Api("appsettings.Development.example.json")).GetProperty("Sms").TryGetProperty("BaseUrl", out _));
        Assert.Contains("/src/*/appsettings.Development.json", File.ReadAllText(Path.Combine(Root(), ".gitignore")));
    }
}
