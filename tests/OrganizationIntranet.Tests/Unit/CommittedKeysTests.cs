using System.Runtime.CompilerServices;
using System.Text.Json;
using Xunit;

namespace OrganizationIntranet.Tests;

/// <summary>
/// کلیدهای توسعه عمداً در appsettings.json ثبت‌شده‌اند (تصمیم صریح)، تا روی هر سیستم بدون User Secrets و بدون فایل محلی کار کنند.
/// این آزمون‌ها فقط سازگاری آن‌ها را می‌سنجند: وجود، طول، یکتایی و هم‌خوانی میان برنامه‌ها. مقدار کلیدها هیچ‌جا چاپ نمی‌شود.
/// برای استقرار واقعی همین مقدارها باید با کلیدهای تازه جایگزین شوند.
/// </summary>
public class CommittedKeysTests
{
    private static string Root([CallerFilePath] string source = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(source)!, "..", "..", ".."));

    private static JsonElement Load(string project)
    {
        var path = Path.Combine(Root(), "src", $"OrganizationIntranet.{project}", "appsettings.json");
        return JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }).RootElement;
    }

    private static string? Str(JsonElement root, params string[] path)
    {
        foreach (var part in path) { if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty(part, out root)) return null; }
        return root.ValueKind == JsonValueKind.String ? root.GetString() : null;
    }

    [Fact]
    public void Api_and_UI_applications_share_committed_client_keys_without_any_local_setup()
    {
        var api = Load("Api"); var portal = Load("Portal"); var admin = Load("Admin");
        var client = Str(api, "Api", "ClientKey");
        Assert.True(client is { Length: >= 32 });
        Assert.Equal(client, Str(portal, "Api", "ClientKey"));
        Assert.Equal(client, Str(admin, "Api", "ClientKey"));

        var adminKey = Str(api, "Api", "AdminClientKey");
        Assert.True(adminKey is { Length: >= 32 });
        Assert.Equal(adminKey, Str(admin, "Api", "AdminClientKey"));            // Admin و API یک کلید مدیریتی دارند
        Assert.NotEqual(client, adminKey);                                       // و با کلید عمومی فرق دارد
        Assert.Null(Str(portal, "Api", "AdminClientKey"));                       // Portal کلید مدیریتی را نمی‌داند
    }

    [Fact]
    public void Committed_sso_keys_are_valid_distinct_and_have_an_absolute_landing_url()
    {
        var api = Load("Api");
        var all = new List<string> { Str(api, "Api", "ClientKey")!, Str(api, "Api", "AdminClientKey")! };
        foreach (var code in new[] { "TASKS", "ITSERVICEDESK", "ACCESSREPORT" })
        {
            var key = Str(api, "ApplicationIntegrations", code, "SsoKey");
            var url = Str(api, "ApplicationIntegrations", code, "SsoUrl");
            Assert.True(key is { Length: >= 32 }, $"{code}: SsoKey is missing or shorter than 32 characters");
            Assert.True(Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" && string.IsNullOrEmpty(uri.UserInfo) && uri.Fragment.Length == 0, $"{code}: SsoUrl is not a valid absolute URL");
            all.Add(key!);
        }
        Assert.Equal(all.Count, all.Distinct(StringComparer.Ordinal).Count());   // هیچ دو کلیدی یکی نیست
    }
}
