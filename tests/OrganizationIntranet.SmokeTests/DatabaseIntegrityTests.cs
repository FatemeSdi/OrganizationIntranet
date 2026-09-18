using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using OrganizationIntranet.Api.Data;
using OrganizationIntranet.Domain.Entities;
using Xunit;

namespace OrganizationIntranet.SmokeTests;

public sealed class DatabaseIntegrityTests
{
    [Fact]
    public async Task Publications_enforce_database_constraints_and_concurrent_writes()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        connection.CreateFunction("GETUTCDATE", () => DateTime.UtcNow);
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using var first = new AppDbContext(options);
        await first.Database.EnsureCreatedAsync();
        var user = new User { Username = "editor", Name = "Test", LastName = "Editor" };
        first.Users.Add(user);
        await first.SaveChangesAsync();
        var publication = new Publication { Title = "Title", Summary = "Summary", Body = "Body", CreatedBy = user.UserId, UpdatedBy = user.UserId };
        first.Publications.Add(publication);
        await first.SaveChangesAsync();
        await using var second = new AppDbContext(options);
        var stale = await second.Publications.SingleAsync();
        publication.Title = "Saved";
        publication.Revision++;
        await first.SaveChangesAsync();
        stale.Title = "Stale";
        stale.Revision++;
        await Assert.ThrowsAsync<OrganizationIntranet.Application.Abstractions.PublicationConflictException>(
            () => new PublicationRepository(second).SaveAsync());
        publication.Kind = "Invalid";
        await Assert.ThrowsAsync<DbUpdateException>(() => first.SaveChangesAsync());
        publication.Kind = "News";
        publication.IsPublished = true;
        await Assert.ThrowsAsync<DbUpdateException>(() => first.SaveChangesAsync());
        publication.PublishedAt = DateTime.UtcNow;
        await first.SaveChangesAsync();
    }

    [Fact]
    public void SqlServer_model_preserves_types_global_permission_codes_and_restrictive_deletes()
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=unused;Database=unused;Integrated Security=true").Options);
        var model = db.GetService<IDesignTimeModel>().Model;
        Assert.All(model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()),
            fk => Assert.Equal(DeleteBehavior.NoAction, fk.DeleteBehavior));
        Assert.Equal("varchar(100)", model.FindEntityType(typeof(Permission))!.FindProperty(nameof(Permission.PermissionCode))!.GetColumnType());
        Assert.Equal("datetime", model.FindEntityType(typeof(UserNotification))!.FindProperty(nameof(UserNotification.ReadAt))!.GetColumnType());
        Assert.Equal("GETUTCDATE()", model.FindEntityType(typeof(RolePermission))!.FindProperty(nameof(RolePermission.CreatedAt))!.GetDefaultValueSql());
        Assert.Contains(model.FindEntityType(typeof(Permission))!.GetIndexes(),
            i => i.IsUnique && i.Properties.Select(p => p.Name).SequenceEqual([nameof(Permission.PermissionCode)]));
    }

    [Fact]
    public async Task Database_rejects_duplicate_grants_cross_application_code_collisions_and_invalid_read_state()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        connection.CreateFunction("GETUTCDATE", () => DateTime.UtcNow);
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var user = new User { Username = "integrity", Name = "Test", LastName = "User" };
        var role = new Role { RoleName = "Test", RoleCode = "TEST" };
        var first = new OrganizationIntranet.Domain.Entities.Application { ApplicationName = "First", ApplicationCode = "FIRST" };
        var second = new OrganizationIntranet.Domain.Entities.Application { ApplicationName = "Second", ApplicationCode = "SECOND" };
        var permission = new Permission { Application = first, PermissionName = "View", PermissionCode = "VIEW" };
        var notification = new Notification { Application = first, Title = "Test", Message = "Test" };
        db.AddRange(user, role, first, second, permission, notification);
        await db.SaveChangesAsync();
        db.UserRoles.Add(new UserRole { UserId = user.UserId, RoleId = role.RoleId });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        db.UserRoles.Add(new UserRole { UserId = user.UserId, RoleId = role.RoleId });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        db.Permissions.Add(new Permission { ApplicationId = second.ApplicationId, PermissionName = "Duplicate", PermissionCode = "VIEW" });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        var recipient = new UserNotification { UserId = user.UserId, NotificationId = notification.NotificationId, IsRead = true };
        db.UserNotifications.Add(recipient);
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        recipient = new UserNotification { UserId = user.UserId, NotificationId = notification.NotificationId };
        db.UserNotifications.Add(recipient);
        await db.SaveChangesAsync();
        recipient.ReadAt = DateTime.UtcNow;
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        recipient.IsRead = true;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        db.Users.Remove(await db.Users.SingleAsync());
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
}
