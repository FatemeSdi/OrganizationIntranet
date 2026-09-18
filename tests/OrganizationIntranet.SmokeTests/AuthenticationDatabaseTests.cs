using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OrganizationIntranet.Api.Data;
using OrganizationIntranet.Domain.Entities;
using Xunit;

namespace OrganizationIntranet.SmokeTests;

public sealed class AuthenticationDatabaseTests
{
    [Fact]
    public async Task Invalid_security_state_is_rejected_and_challenges_cannot_be_consumed_twice()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        connection.CreateFunction("GETUTCDATE", () => DateTime.UtcNow);
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using var db = new AppDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var user = new User { Username = "schema-test", Name = "Schema", LastName = "Test" };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        db.Add(new AuthenticationSettings { Id = 2 });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        var challenge = new AuthenticationChallenge { Id = new string('A', 64), UserId = user.UserId, Purpose = "invalid", ExpiresAt = DateTime.UtcNow.AddMinutes(5) };
        db.Add(challenge);
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        challenge.Purpose = "login";
        challenge.Attempts = -1;
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        challenge.Attempts = 0;
        await db.SaveChangesAsync();
        await using var concurrent = new AppDbContext(options);
        var stale = await concurrent.Set<AuthenticationChallenge>().SingleAsync();
        challenge.Consumed = true;
        await new OrganizationIntranet.Api.Data.AuthenticationRepository(db).SaveAsync();
        stale.Consumed = true;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => new OrganizationIntranet.Api.Data.AuthenticationRepository(concurrent).SaveAsync());
    }
}
