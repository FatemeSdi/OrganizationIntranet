using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using OrganizationIntranet.Api.Data;
using Xunit;

namespace OrganizationIntranet.SmokeTests;

public sealed class LiveSchemaFactAttribute : FactAttribute
{
    public LiveSchemaFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("INTRANET_SCHEMA_TEST_CONNECTION")))
            Skip = "Opt-in read-only SQL Server schema check; requires INTRANET_SCHEMA_TEST_CONNECTION.";
    }
}

public sealed class LiveDatabaseSchemaTests
{
    [LiveSchemaFact]
    public async Task Every_mapped_column_matches_the_live_SQL_Server_schema()
    {
        var connectionString = Environment.GetEnvironmentVariable("INTRANET_SCHEMA_TEST_CONNECTION")!;
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connectionString).Options);
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT s.name,t.name,c.name,ty.name,c.max_length,c.precision,c.scale,c.is_nullable
            FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id
            JOIN sys.columns c ON c.object_id=t.object_id JOIN sys.types ty ON ty.user_type_id=c.user_type_id
            WHERE t.is_ms_shipped=0
            """;
        var actual = new Dictionary<string, (string Type, bool Nullable)>();
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                var type = reader.GetString(3);
                var length = reader.GetInt16(4);
                var storeType = type switch
                {
                    "nvarchar" or "nchar" => $"{type}({(length == -1 ? "max" : (length / 2).ToString())})",
                    "varchar" or "char" or "varbinary" or "binary" => $"{type}({(length == -1 ? "max" : length.ToString())})",
                    "decimal" or "numeric" => $"{type}({reader.GetByte(5)},{reader.GetByte(6)})",
                    "datetime2" or "datetimeoffset" or "time" => $"{type}({reader.GetByte(6)})",
                    _ => type
                };
                actual[$"{reader.GetString(0)}.{reader.GetString(1)}.{reader.GetString(2)}"] = (storeType, reader.GetBoolean(7));
            }
        }
        var failures = new List<string>();
        foreach (var entity in db.Model.GetEntityTypes())
        {
            var table = StoreObjectIdentifier.Table(entity.GetTableName()!, entity.GetSchema());
            foreach (var property in entity.GetProperties())
            {
                var key = $"{entity.GetSchema() ?? "dbo"}.{entity.GetTableName()}.{property.GetColumnName(table)}";
                var expected = property.GetColumnType()!;
                if (expected is "datetime2" or "datetimeoffset" or "time") expected += "(7)";
                if (!actual.TryGetValue(key, out var column)) failures.Add($"Missing {key}");
                else if (!string.Equals(expected, column.Type, StringComparison.OrdinalIgnoreCase) || property.IsColumnNullable(table) != column.Nullable)
                    failures.Add($"{key}: expected {expected}, nullable={property.IsColumnNullable(table)}; actual {column.Type}, nullable={column.Nullable}");
            }
        }
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }
}
