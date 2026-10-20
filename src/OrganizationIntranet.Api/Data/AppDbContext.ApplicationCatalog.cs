using Microsoft.EntityFrameworkCore;
using OrganizationIntranet.Domain.Entities;

namespace OrganizationIntranet.Api.Data;

public partial class AppDbContext
{
    public DbSet<ApplicationAccess> ApplicationAccessGrants => Set<ApplicationAccess>();
    public DbSet<ApplicationServiceRegistration> ApplicationServices => Set<ApplicationServiceRegistration>();

    private static void ConfigureApplicationCatalog(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Domain.Entities.Application>().Property(a => a.RequiresLogin).HasDefaultValue(true);
        modelBuilder.Entity<Domain.Entities.Application>().Property(a => a.IsInternetAccessible).HasDefaultValue(false);
        modelBuilder.Entity<Domain.Entities.Application>().Property(a => a.IsPublic).HasDefaultValue(false);
        modelBuilder.Entity<ApplicationAccess>(e =>
        {
            e.ToTable("ApplicationAccess", "App", t => t.HasCheckConstraint("CK_ApplicationAccess_Principal",
                "([UserId] IS NOT NULL AND [RoleId] IS NULL) OR ([UserId] IS NULL AND [RoleId] IS NOT NULL)"));
            e.HasKey(x => x.ApplicationAccessId);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            e.HasIndex(x => new { x.ApplicationId, x.UserId }).IsUnique().HasFilter("[UserId] IS NOT NULL");
            e.HasIndex(x => new { x.ApplicationId, x.RoleId }).IsUnique().HasFilter("[RoleId] IS NOT NULL");
            e.HasOne(x => x.Application).WithMany(x => x.AccessGrants).HasForeignKey(x => x.ApplicationId).OnDelete(DeleteBehavior.NoAction);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
            e.HasOne(x => x.Role).WithMany().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.NoAction);
        });
        modelBuilder.Entity<ApplicationServiceRegistration>(e =>
        {
            e.ToTable("ApplicationService", "App");
            e.HasKey(x => x.ApplicationServiceRegistrationId);
            e.Property(x => x.ServiceCode).HasMaxLength(50).IsUnicode(false).IsRequired();
            e.Property(x => x.DisplayName).HasMaxLength(150).IsRequired();
            e.Property(x => x.EndpointUrl).HasMaxLength(500);
            e.Property(x => x.IsActive).HasDefaultValue(true);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            e.HasIndex(x => new { x.ApplicationId, x.ServiceCode }).IsUnique();
            e.Property(x => x.SyncCursor).HasMaxLength(1000).IsConcurrencyToken();
            e.Property(x => x.LastSyncError).HasMaxLength(500);
            e.HasOne(x => x.Application).WithMany(x => x.Services).HasForeignKey(x => x.ApplicationId).OnDelete(DeleteBehavior.NoAction);
        });
    }
}
