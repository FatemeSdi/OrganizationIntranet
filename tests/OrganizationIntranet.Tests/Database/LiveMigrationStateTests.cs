using Microsoft.Data.SqlClient;
using Xunit;

namespace OrganizationIntranet.Tests;

/// <summary>
/// بررسی فقط‌خواندنیِ بانک واقعی (اختیاری با INTRANET_SCHEMA_TEST_CONNECTION): همه migrationهای نسخه‌دار مخزن باید در dbo.SchemaMigration ثبت شده باشند.
/// هنگام افزودن migration جدید، شناسه‌ی آن را به ExpectedMigrations اضافه کنید.
/// </summary>
[Trait("Category", "Database")]
public sealed class LiveMigrationStateTests
{
    // 003 و 004 باید روی هر بانکی که کد فعلی را اجرا می‌کند اعمال شده باشند (004 به 003 وابسته است).
    private static readonly string[] ExpectedMigrations =
    [
        "001_DatabaseConsistency",
        "002_AuthenticationSchema",
        "003_ApplicationCatalog",
        "004_AccessWorkflow",
        "005_PublicPublications",
    ];

    [LiveSchemaFact]
    public async Task Every_repository_migration_is_applied_to_the_live_database()
    {
        await using var connection = new SqlConnection(Environment.GetEnvironmentVariable("INTRANET_SCHEMA_TEST_CONNECTION")!);
        await connection.OpenAsync();
        await using var command = new SqlCommand("SELECT MigrationId FROM dbo.SchemaMigration", connection);
        var applied = new HashSet<string>(StringComparer.Ordinal);
        await using (var reader = await command.ExecuteReaderAsync())
            while (await reader.ReadAsync()) applied.Add(reader.GetString(0));

        var missing = ExpectedMigrations.Where(id => !applied.Contains(id)).ToArray();
        Assert.True(missing.Length == 0, "Migrations not applied to the live database: " + string.Join(", ", missing));
    }
}
