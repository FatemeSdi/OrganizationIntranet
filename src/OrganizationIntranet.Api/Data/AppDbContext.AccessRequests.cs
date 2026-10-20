using Microsoft.EntityFrameworkCore;
using OrganizationIntranet.Domain.Entities;

namespace OrganizationIntranet.Api.Data;

public partial class AppDbContext
{
    public DbSet<ApplicationAccessRequest> ApplicationAccessRequests => Set<ApplicationAccessRequest>();
    private static void ConfigureAccessRequests(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ApplicationAccessRequest>(e =>
        {
            e.ToTable("ApplicationAccessRequest", "App", t =>
            {
                t.HasCheckConstraint("CK_ApplicationAccessRequest_Status", "[Status] IN ('Pending','Approved','Rejected','Cancelled')");
                t.HasCheckConstraint("CK_ApplicationAccessRequest_Resolution", "([Status] = 'Pending' AND [ResolvedAt] IS NULL AND [ReviewedBy] IS NULL) OR ([Status] = 'Cancelled' AND [ResolvedAt] IS NOT NULL AND [ReviewedBy] IS NULL) OR ([Status] IN ('Approved','Rejected') AND [ResolvedAt] IS NOT NULL AND [ReviewedBy] IS NOT NULL)");
            });
            e.HasKey(r => r.ApplicationAccessRequestId);
            e.Property(r => r.Reason).HasMaxLength(1000).IsRequired();
            e.Property(r => r.ReviewNote).HasMaxLength(1000);
            e.Property(r => r.Status).HasMaxLength(20).IsUnicode(false).IsRequired();
            e.Property(r => r.Revision).IsConcurrencyToken();
            e.Property(r => r.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            e.HasIndex(r => new { r.UserId, r.ApplicationId }).IsUnique().HasFilter("[Status] = 'Pending'");
            e.HasIndex(r => new { r.Status, r.CreatedAt, r.ApplicationAccessRequestId });
            e.HasOne(r => r.Application).WithMany().HasForeignKey(r => r.ApplicationId).OnDelete(DeleteBehavior.NoAction);
            e.HasOne(r => r.User).WithMany().HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.NoAction);
            e.HasOne(r => r.Reviewer).WithMany().HasForeignKey(r => r.ReviewedBy).OnDelete(DeleteBehavior.NoAction);
        });
    }
}
