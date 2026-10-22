using Microsoft.EntityFrameworkCore;
using OrganizationIntranet.Api.Data;
using OrganizationIntranet.Application.Abstractions;
using Xunit;

namespace OrganizationIntranet.Tests;

/// <summary>
/// کوئری UNION ALL مرکز اعلان و کارتابل نظارت روی SQL Server واقعی هم ترجمه و اجرا می‌شود (آزمون‌های Integration روی SQLite‌اند).
/// فقط SELECT؛ اختیاری با INTRANET_SCHEMA_TEST_CONNECTION.
/// </summary>
[Trait("Category", "Database")]
public sealed class LiveNotificationCenterTests
{
    [LiveSchemaFact]
    public async Task Notification_center_and_supervision_queries_run_on_SQL_Server()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(Environment.GetEnvironmentVariable("INTRANET_SCHEMA_TEST_CONNECTION")!).Options);
        var userIds = await db.Users.AsNoTracking().OrderBy(u => u.UserId).Select(u => u.UserId).Take(5).ToListAsync();
        Assert.NotEmpty(userIds);
        var center = new NotificationCenterRepository(db);
        var supervision = new SupervisionRepository(db);
        foreach (var userId in userIds)
        {
            foreach (var query in new[] { new NotificationCenterQuery(null, null, null, 1, 20), new NotificationCenterQuery(null, false, DateTime.UtcNow.AddDays(-30), 2, 20),
                         new NotificationCenterQuery("system", true, null, 1, 20), new NotificationCenterQuery("request", null, DateTime.UtcNow.AddDays(-7), 1, 20) })
            {
                var (items, total, all, unread) = await center.PageAsync(userId, query);
                Assert.InRange(unread, 0, all);
                Assert.InRange(total, 0, all);
                Assert.True(items.Count <= 20);
                Assert.Equal(items.OrderByDescending(i => i.CreatedAt).ThenByDescending(i => i.Id).Select(i => (i.Kind, i.Id)), items.Select(i => (i.Kind, i.Id)));
            }
            var (apps, counts) = await supervision.InboxSummaryAsync(userId, null);
            var (requests, requestTotal) = await supervision.InboxAsync(userId, 1, 20, "Pending", apps.FirstOrDefault().Id);
            Assert.All(requests, r => Assert.Equal("Pending", r.Status));
            Assert.True(requestTotal <= counts.Values.Sum());
        }
        Assert.Empty(db.ChangeTracker.Entries()); // فقط‌خواندنی
    }
}
