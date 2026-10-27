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
    internal static readonly string[] ExpectedMigrations =
    [
        "001_DatabaseConsistency",
        "002_AuthenticationSchema",
        "003_ApplicationCatalog",
        "004_AccessWorkflow",
        "005_PublicPublications",
        "006_NationalIdLogin",
        "007_PublicationImages",
        "008_AdminPermissions",
        "009_SsoTickets",
        "010_PublicationArchive",
        "011_UserApplicationAccounts",
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

    /// <summary>فهرست کد (AdminPermissions) و migration 008 یکی‌اند: نقش فعال ADMIN همه‌ی مجوزهای فعال پنل را دارد و کسی از پنل قفل نیست.</summary>
    [LiveSchemaFact]
    public async Task Admin_role_holds_every_admin_panel_permission_in_the_live_database()
    {
        await using var connection = new SqlConnection(Environment.GetEnvironmentVariable("INTRANET_SCHEMA_TEST_CONNECTION")!);
        await connection.OpenAsync();
        await using var command = new SqlCommand("""
            SELECT p.PermissionCode, p.PermissionName, p.Description FROM Sec.Permission p JOIN Sec.RolePermission rp ON rp.PermissionId=p.PermissionId
            JOIN Sec.Role r ON r.RoleId=rp.RoleId WHERE r.RoleCode='ADMIN' AND r.IsActive=1 AND p.IsActive=1;
            SELECT COUNT(DISTINCT u.UserId) FROM Sec.[User] u JOIN Sec.UserRole ur ON ur.UserId=u.UserId JOIN Sec.Role r ON r.RoleId=ur.RoleId AND r.IsActive=1
            WHERE u.IsActive=1 AND r.RoleCode='ADMIN';
            """, connection);
        var codes = new HashSet<string>(StringComparer.Ordinal);
        var garbled = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            codes.Add(reader.GetString(0));
            // UTF-8 فارسی که با کدپیج ANSI خوانده شود با Ø/Ù/Ú/Û شروع می‌شود (اتفاقی که برای 008 با PowerShell 5.1 افتاد).
            var text = reader.GetString(1) + (reader.IsDBNull(2) ? "" : reader.GetString(2));
            if (text.IndexOfAny(['Ø', 'Ù', 'Ú', 'Û']) >= 0) garbled.Add(reader.GetString(0));
        }
        await reader.NextResultAsync(); await reader.ReadAsync();
        var admins = reader.GetInt32(0);
        var missing = OrganizationIntranet.Contracts.AdminPermissions.All.Select(p => p.Code).Where(c => !codes.Contains(c)).ToArray();
        Assert.True(missing.Length == 0, "ADMIN lacks: " + string.Join(", ", missing));
        Assert.True(admins > 0, "No active user holds the ADMIN role.");
        Assert.True(garbled.Count == 0, "Garbled permission text: " + string.Join(", ", garbled));
    }
}
