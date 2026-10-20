using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OrganizationIntranet.Api.Data;
using OrganizationIntranet.Application.Abstractions;
using OrganizationIntranet.Domain.Entities;
using Xunit;

namespace OrganizationIntranet.Tests;

[Trait("Category", "Database")]
public sealed class PublicationPublicConstraintTests
{
    private static async Task<(SqliteConnection Connection, AppDbContext Db, long UserId)> OpenAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        connection.CreateFunction("GETUTCDATE", () => DateTime.UtcNow);
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var user = new User { Username = "editor", Name = "Test", LastName = "Editor" };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return (connection, db, user.UserId);
    }

    [Fact]
    public async Task Public_content_must_be_published_at_the_database_level()
    {
        var (connection, db, userId) = await OpenAsync();
        using (connection) await using (db)
        {
            var item = new Publication { Title = "T", Summary = "S", Body = "B", CreatedBy = userId, UpdatedBy = userId, IsPublic = true }; // پیش‌نویسِ عمومی
            db.Publications.Add(item);
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            item.IsPublished = true; item.PublishedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(); // منتشرشده و عمومی مجاز است
            Assert.True((await db.Publications.AsNoTracking().SingleAsync()).IsPublic);
        }
    }

    [Fact]
    public async Task New_content_is_private_by_default_and_repository_scopes_are_enforced()
    {
        var (connection, db, userId) = await OpenAsync();
        using (connection) await using (db)
        {
            var now = DateTime.UtcNow;
            Publication Make(string title, bool published, bool pub) => new() { Title = title, Summary = "S", Body = "B", CreatedBy = userId, UpdatedBy = userId, IsPublished = published, IsPublic = pub, PublishedAt = published ? now : null };
            db.Publications.AddRange(Make("draft", false, false), Make("internal", true, false), Make("public", true, true));
            await db.SaveChangesAsync();
            Assert.False(new Publication().IsPublic);
            var repository = new PublicationRepository(db);
            Assert.Equal(["draft", "internal", "public"], (await repository.ListAsync(PublicationScope.All, null, null, 1, 20)).Items.Select(p => p.Title).Order().ToArray());
            Assert.Equal(["internal", "public"], (await repository.ListAsync(PublicationScope.Published, null, null, 1, 20)).Items.Select(p => p.Title).Order().ToArray());
            var pubOnly = await repository.ListAsync(PublicationScope.Public, null, null, 1, 20);
            Assert.Equal("public", Assert.Single(pubOnly.Items).Title);
            Assert.Equal(1, pubOnly.Total);
            Assert.Null(await repository.FindAsync((await db.Publications.SingleAsync(p => p.Title == "internal")).PublicationId, PublicationScope.Public));
            Assert.Null(await repository.FindAsync((await db.Publications.SingleAsync(p => p.Title == "draft")).PublicationId, PublicationScope.Published));
        }
    }
}
