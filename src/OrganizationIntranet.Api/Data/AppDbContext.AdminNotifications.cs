using Microsoft.EntityFrameworkCore;
using OrganizationIntranet.Domain.Entities;

namespace OrganizationIntranet.Api.Data;

/// <summary>بخش اعلان‌های سازمانی ارسالی مدیر از DbContext: فعال/غیرفعال، گیرنده (کاربر، واحد یا همه) و شمار گیرندگان؛ migration 015_AdminNotifications.</summary>
public partial class AppDbContext
{
    private static void ConfigureAdminNotifications(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Notification>(e =>
        {
            e.ToTable("Notification", "Notify", t => t.HasCheckConstraint("CK_Notification_Audience",
                "([AudienceType] IS NULL AND [AudienceUserId] IS NULL AND [AudienceUnitId] IS NULL AND [RecipientCount] IS NULL) "
                + "OR ([AudienceType] = 'All' AND [AudienceUserId] IS NULL AND [AudienceUnitId] IS NULL AND [CreatedBy] IS NOT NULL AND [RecipientCount] >= 1) "
                + "OR ([AudienceType] = 'User' AND [AudienceUserId] IS NOT NULL AND [AudienceUnitId] IS NULL AND [CreatedBy] IS NOT NULL AND [RecipientCount] >= 1) "
                + "OR ([AudienceType] = 'Unit' AND [AudienceUnitId] IS NOT NULL AND [AudienceUserId] IS NULL AND [CreatedBy] IS NOT NULL AND [RecipientCount] >= 1)"));
            e.Property(x => x.IsActive).HasDefaultValue(true);
            e.Property(x => x.AudienceType).HasMaxLength(10).IsUnicode(false);
            e.HasIndex(x => x.AudienceUserId).HasFilter("[AudienceUserId] IS NOT NULL").HasDatabaseName("IX_Notification_AudienceUserId");
            e.HasIndex(x => x.AudienceUnitId).HasFilter("[AudienceUnitId] IS NOT NULL").HasDatabaseName("IX_Notification_AudienceUnitId");
            e.HasOne<User>().WithMany().HasForeignKey(x => x.AudienceUserId).HasConstraintName("FK_Notification_AudienceUser").OnDelete(DeleteBehavior.NoAction);
            e.HasOne<OrgUnit>().WithMany().HasForeignKey(x => x.AudienceUnitId).HasConstraintName("FK_Notification_AudienceUnit").OnDelete(DeleteBehavior.NoAction);
        });
    }
}
