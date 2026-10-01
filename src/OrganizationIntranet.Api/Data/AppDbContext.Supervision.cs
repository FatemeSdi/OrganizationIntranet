using Microsoft.EntityFrameworkCore;
using OrganizationIntranet.Domain.Entities;

namespace OrganizationIntranet.Api.Data;

public partial class AppDbContext
{
    public DbSet<ApplicationSupervisionGroup> ApplicationSupervisionGroups => Set<ApplicationSupervisionGroup>();
    public DbSet<ApplicationSupervisor> ApplicationSupervisors => Set<ApplicationSupervisor>();
    public DbSet<AccessRequestAlert> AccessRequestAlerts => Set<AccessRequestAlert>();
    private static void ConfigureSupervision(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Domain.Entities.Application>(e =>
        {
            e.Property(a => a.AccessRequestNotificationChannels).HasMaxLength(10).IsUnicode(false).HasDefaultValue("InApp");
            e.ToTable("Application", "App", t => t.HasCheckConstraint("CK_Application_AccessRequestChannels", "[AccessRequestNotificationChannels] IN ('InApp','Sms','Both')"));
        });
        modelBuilder.Entity<ApplicationSupervisionGroup>(e =>
        {
            e.ToTable("ApplicationSupervisionGroup", "App"); e.HasKey(g => g.ApplicationSupervisionGroupId);
            e.Property(g => g.Name).HasMaxLength(150).IsRequired(); e.Property(g => g.Revision).IsConcurrencyToken();
            e.HasIndex(g => new { g.ApplicationId, g.Name }).IsUnique();
            e.HasOne(g => g.Application).WithMany(a => a.SupervisionGroups).HasForeignKey(g => g.ApplicationId).OnDelete(DeleteBehavior.NoAction);
        });
        modelBuilder.Entity<ApplicationSupervisor>(e =>
        {
            e.ToTable("ApplicationSupervisor", "App", t => t.HasCheckConstraint("CK_ApplicationSupervisor_Principal", "([UserId] IS NOT NULL AND [RoleId] IS NULL) OR ([UserId] IS NULL AND [RoleId] IS NOT NULL)"));
            e.HasKey(m => m.ApplicationSupervisorId);
            e.HasIndex(m => new { m.ApplicationSupervisionGroupId, m.UserId }).IsUnique().HasFilter("[UserId] IS NOT NULL");
            e.HasIndex(m => new { m.ApplicationSupervisionGroupId, m.RoleId }).IsUnique().HasFilter("[RoleId] IS NOT NULL");
            e.HasOne(m => m.Group).WithMany(g => g.Members).HasForeignKey(m => m.ApplicationSupervisionGroupId).OnDelete(DeleteBehavior.NoAction);
            e.HasOne(m => m.User).WithMany().HasForeignKey(m => m.UserId).OnDelete(DeleteBehavior.NoAction);
            e.HasOne(m => m.Role).WithMany().HasForeignKey(m => m.RoleId).OnDelete(DeleteBehavior.NoAction);
        });
        modelBuilder.Entity<AccessRequestAlert>(e =>
        {
            e.ToTable("AccessRequestAlert", "Notify", t =>
            {
                t.HasCheckConstraint("CK_AccessRequestAlert_ReadState", "([IsRead] = 0 AND [ReadAt] IS NULL) OR ([IsRead] = 1 AND [ReadAt] IS NOT NULL)");
                t.HasCheckConstraint("CK_AccessRequestAlert_SmsStatus", "[SmsStatus] IN ('NotRequested','Pending','Processing','Sent','Failed','Cancelled')");
            });
            e.HasKey(a => a.AccessRequestAlertId); e.Property(a => a.Revision).IsConcurrencyToken();
            e.Property(a => a.SmsStatus).HasMaxLength(20).IsUnicode(false).IsRequired(); e.Property(a => a.SmsError).HasMaxLength(500);
            e.HasIndex(a => new { a.ApplicationAccessRequestId, a.UserId }).IsUnique();
            e.HasIndex(a => new { a.UserId, a.InApp, a.IsRead }); e.HasIndex(a => new { a.SmsStatus, a.NextAttemptAt });
            e.HasOne(a => a.Request).WithMany().HasForeignKey(a => a.ApplicationAccessRequestId).OnDelete(DeleteBehavior.NoAction);
            e.HasOne(a => a.User).WithMany().HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.NoAction);
        });
    }
}
