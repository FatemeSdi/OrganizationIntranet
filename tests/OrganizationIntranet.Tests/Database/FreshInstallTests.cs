using System.Runtime.CompilerServices;
using Microsoft.Data.SqlClient;
using Xunit;

namespace OrganizationIntranet.Tests;

/// <summary>
/// نصب تازه روی یک بانک موقت و خالی؛ بانک واقعی را تغییر نمی‌دهد. اختیاری: INTRANET_FRESH_INSTALL_SERVER باید به سروری اشاره کند
/// که آزمون اجازه‌ی ساخت و حذف بانک موقت روی آن را دارد (مثلاً LocalDB:
/// <c>Server=(localdb)\MSSQLLocalDB;Integrated Security=True;TrustServerCertificate=True</c>).
/// </summary>
public sealed class FreshInstallFactAttribute : FactAttribute
{
    public FreshInstallFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("INTRANET_FRESH_INSTALL_SERVER")))
            Skip = "Opt-in fresh-install check; requires INTRANET_FRESH_INSTALL_SERVER (a server where a scratch database may be created and dropped).";
    }
}

[Trait("Category", "Database")]
public sealed class FreshInstallTests
{
    // ساختار قابل‌مقایسه‌ی بانک: ستون‌ها (با پیش‌فرض)، ایندکس‌ها، FK و CHECK؛ نام قیدها هم جزو آن است.
    private const string FingerprintSql = """
        SET NOCOUNT ON;
        WITH t AS (SELECT t.object_id, s.name + '.' + t.name AS tn FROM sys.tables t JOIN sys.schemas s ON s.schema_id = t.schema_id WHERE t.is_ms_shipped = 0 AND t.name <> 'sysdiagrams')
        SELECT line FROM (
         SELECT 'COL|' + t.tn + '|' + c.name + '|' + ty.name + '(' + CONVERT(varchar(10), c.max_length) + ',' + CONVERT(varchar(10), c.precision) + ',' + CONVERT(varchar(10), c.scale) + ')|'
           + CASE c.is_nullable WHEN 1 THEN 'NULL' ELSE 'NOT NULL' END + '|' + CASE c.is_identity WHEN 1 THEN 'IDENTITY' ELSE '' END + '|' + ISNULL(dc.name + '=' + dc.definition, '') AS line
         FROM t JOIN sys.columns c ON c.object_id = t.object_id JOIN sys.types ty ON ty.user_type_id = c.user_type_id LEFT JOIN sys.default_constraints dc ON dc.object_id = c.default_object_id
         UNION ALL
         SELECT 'IDX|' + t.tn + '|' + i.name + '|' + i.type_desc COLLATE DATABASE_DEFAULT + '|' + CASE WHEN i.is_primary_key = 1 THEN 'PK' WHEN i.is_unique_constraint = 1 THEN 'UQ' WHEN i.is_unique = 1 THEN 'UNIQUE' ELSE '' END + '|' + ISNULL(i.filter_definition, '') + '|'
           + STUFF((SELECT ',' + COL_NAME(ic.object_id, ic.column_id) + CASE WHEN ic.is_included_column = 1 THEN '+' WHEN ic.is_descending_key = 1 THEN ' DESC' ELSE '' END
                    FROM sys.index_columns ic WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id ORDER BY ic.is_included_column, ic.key_ordinal, ic.column_id FOR XML PATH('')), 1, 1, '')
         FROM t JOIN sys.indexes i ON i.object_id = t.object_id WHERE i.type > 0
         UNION ALL
         SELECT 'FK|' + t.tn + '|' + fk.name + '|' + OBJECT_SCHEMA_NAME(fk.referenced_object_id) + '.' + OBJECT_NAME(fk.referenced_object_id) + '|'
           + STUFF((SELECT ',' + COL_NAME(fc.parent_object_id, fc.parent_column_id) + '>' + COL_NAME(fc.referenced_object_id, fc.referenced_column_id) FROM sys.foreign_key_columns fc WHERE fc.constraint_object_id = fk.object_id ORDER BY fc.constraint_column_id FOR XML PATH('')), 1, 1, '')
           + '|' + fk.delete_referential_action_desc COLLATE DATABASE_DEFAULT + '|' + fk.update_referential_action_desc COLLATE DATABASE_DEFAULT + '|' + CASE WHEN fk.is_not_trusted = 1 OR fk.is_disabled = 1 THEN 'UNTRUSTED' ELSE 'TRUSTED' END
         FROM t JOIN sys.foreign_keys fk ON fk.parent_object_id = t.object_id
         UNION ALL
         SELECT 'CK|' + t.tn + '|' + ck.name + '|' + ck.definition + '|' + CASE WHEN ck.is_not_trusted = 1 OR ck.is_disabled = 1 THEN 'UNTRUSTED' ELSE 'TRUSTED' END
         FROM t JOIN sys.check_constraints ck ON ck.parent_object_id = t.object_id
        ) x ORDER BY line;
        """;

    private static string Migrations([CallerFilePath] string source = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(source)!, "..", "..", "..", "database", "migrations"));

    private static async Task RunFileAsync(string connectionString, string file)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(await File.ReadAllTextAsync(Path.Combine(Migrations(), file)), connection) { CommandTimeout = 120 };
        try { await command.ExecuteNonQueryAsync(); }
        catch (SqlException ex) { throw new InvalidOperationException($"{file}: {ex.Message}", ex); }
    }
    private static async Task<List<string>> QueryAsync(string connectionString, string sql)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        var rows = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        do { while (await reader.ReadAsync()) rows.Add(reader.GetString(0)); } while (await reader.NextResultAsync());
        return rows;
    }
    private static string Report(IEnumerable<string> expected, IEnumerable<string> actual) =>
        string.Join(Environment.NewLine, expected.Except(actual).Select(l => "- " + l).Concat(actual.Except(expected).Select(l => "+ " + l)));

    [FreshInstallFact]
    public async Task Baseline_plus_every_migration_builds_the_current_schema_and_rolls_back_cleanly()
    {
        var server = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("INTRANET_FRESH_INSTALL_SERVER")!);
        var name = $"OrganizationIntranet_FreshInstall_{Guid.NewGuid():N}";
        var master = new SqlConnectionStringBuilder(server.ConnectionString) { InitialCatalog = "master" }.ConnectionString;
        var scratch = new SqlConnectionStringBuilder(server.ConnectionString) { InitialCatalog = name, Pooling = false }.ConnectionString;
        await QueryAsync(master, $"CREATE DATABASE [{name}]; SELECT N'created'");
        try
        {
            var chain = LiveMigrationStateTests.ExpectedMigrations.SkipWhile(id => id != "003_ApplicationCatalog").ToArray();
            await RunFileAsync(scratch, "000_Baseline.install.sql");
            var baseline = await QueryAsync(scratch, FingerprintSql);
            await Assert.ThrowsAsync<InvalidOperationException>(() => RunFileAsync(scratch, "000_Baseline.install.sql")); // فقط روی بانک خالی

            foreach (var id in chain)
            {
                await RunFileAsync(scratch, id + ".up.sql");
                if (id == "008_AdminPermissions") // verify 008 یک مدیر فعال می‌خواهد؛ فقط در بانک موقت
                    await QueryAsync(scratch, """
                        INSERT Sec.[User](Username,Name,LastName,IsActive) VALUES(N'fresh.install.admin',N'نصب',N'آزمایشی',1);
                        INSERT Sec.UserRole(UserId,RoleId) SELECT u.UserId,r.RoleId FROM Sec.[User] u CROSS JOIN Sec.Role r WHERE u.Username=N'fresh.install.admin' AND r.RoleCode='ADMIN';
                        SELECT N'ok'
                        """);
                await RunFileAsync(scratch, id + ".verify.sql");
            }
            foreach (var id in chain) await RunFileAsync(scratch, id + ".up.sql"); // اجرای دوباره بی‌اثر است
            Assert.Equal(LiveMigrationStateTests.ExpectedMigrations, await QueryAsync(scratch, "SELECT MigrationId FROM dbo.SchemaMigration ORDER BY MigrationId"));
            var installed = await QueryAsync(scratch, FingerprintSql);

            var mismatches = await LiveDatabaseSchemaTests.ModelMismatchesAsync(scratch);
            Assert.True(mismatches.Count == 0, string.Join(Environment.NewLine, mismatches));
            var names = await QueryAsync(scratch, "SELECT PermissionName FROM Sec.Permission WHERE PermissionCode='ROLE_EDIT'");
            Assert.Equal("ویرایش نقش‌ها", Assert.Single(names)); // متن فارسی migrationها سالم ذخیره می‌شود

            // اگر بانک مرجع هم داده شده باشد، نصب تازه دقیقاً همان ساختار را دارد
            if (Environment.GetEnvironmentVariable("INTRANET_SCHEMA_TEST_CONNECTION") is { Length: > 0 } live)
            {
                var reference = await QueryAsync(live, FingerprintSql);
                Assert.True(reference.SequenceEqual(installed), "Fresh install differs from the reference database:" + Environment.NewLine + Report(reference, installed));
            }

            // بازگشت 008 تا 003 دقیقاً به خط پایه و نصب دوباره به همان ساختار می‌رسد
            await QueryAsync(scratch, "DELETE Sec.UserRole; DELETE Sec.[User]; SELECT N'ok'");
            foreach (var id in chain.Reverse()) await RunFileAsync(scratch, id + ".down.sql");
            var rolledBack = await QueryAsync(scratch, FingerprintSql);
            Assert.True(baseline.SequenceEqual(rolledBack), "Rollback to 002 differs from the baseline:" + Environment.NewLine + Report(baseline, rolledBack));
            foreach (var id in chain) await RunFileAsync(scratch, id + ".up.sql");
            var reinstalled = await QueryAsync(scratch, FingerprintSql);
            Assert.True(installed.SequenceEqual(reinstalled), "Reinstall differs:" + Environment.NewLine + Report(installed, reinstalled));
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await QueryAsync(master, $"IF DB_ID(N'{name}') IS NOT NULL BEGIN ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}]; END; SELECT N'dropped'");
        }
    }
}
