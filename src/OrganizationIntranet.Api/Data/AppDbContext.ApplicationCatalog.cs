using Microsoft.EntityFrameworkCore;
using OrganizationIntranet.Domain.Entities;

namespace OrganizationIntranet.Api.Data;

/// <summary>بخش کاتالوگ سامانه‌ها از DbContext: دسترسی کاربران و نقش‌ها و سرویس‌های هر سامانه.</summary>
public partial class AppDbContext
{
    public DbSet<ApplicationAccess> ApplicationAccessGrants => Set<ApplicationAccess>();
    public DbSet<ApplicationUnitAccess> ApplicationUnitAccess => Set<ApplicationUnitAccess>();
    public DbSet<ApplicationServiceRegistration> ApplicationServices => Set<ApplicationServiceRegistration>();
    public DbSet<ApplicationImage> ApplicationImages => Set<ApplicationImage>();

    private static void ConfigureApplicationCatalog(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Domain.Entities.Application>().Property(a => a.RequiresLogin).HasDefaultValue(true);
        modelBuilder.Entity<Domain.Entities.Application>().Property(a => a.IsInternetAccessible).HasDefaultValue(false);
        modelBuilder.Entity<Domain.Entities.Application>().Property(a => a.IsPublic).HasDefaultValue(false);
        modelBuilder.Entity<Domain.Entities.Application>().Property(a => a.IsUnitRestricted).HasDefaultValue(false);
        // حالت دسترسی «عمومی» و «محدود به واحد» هم‌زمان نمی‌شوند؛ migration 014_ApplicationUnitAccess.
        modelBuilder.Entity<Domain.Entities.Application>().ToTable("Application", "App", t => t.HasCheckConstraint("CK_Application_AccessMode", "NOT ([IsPublic] = 1 AND [IsUnitRestricted] = 1)"));
        modelBuilder.Entity<ApplicationUnitAccess>(e =>
        {
            e.ToTable("ApplicationUnitAccess", "App");
            e.HasKey(x => x.ApplicationUnitAccessId);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            e.HasIndex(x => new { x.ApplicationId, x.UnitId }).IsUnique().HasDatabaseName("UX_ApplicationUnitAccess_Application_Unit");
            e.HasIndex(x => x.UnitId).HasDatabaseName("IX_ApplicationUnitAccess_UnitId");
            e.HasOne(x => x.Application).WithMany(x => x.UnitAccess).HasForeignKey(x => x.ApplicationId).OnDelete(DeleteBehavior.NoAction);
            e.HasOne(x => x.Unit).WithMany().HasForeignKey(x => x.UnitId).OnDelete(DeleteBehavior.NoAction);
        });
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
        // تصویر کارت سامانه؛ migration 020_ApplicationImages.
        modelBuilder.Entity<ApplicationImage>(e =>
        {
            // قید اندازه (CK_ApplicationImage_Size) فقط در migration است؛ DATALENGTH در SQLite آزمون‌ها وجود ندارد و سرویس هم اندازه را می‌سنجد.
            e.ToTable("ApplicationImage", "App", t => t.HasCheckConstraint("CK_ApplicationImage_ContentType", "[ContentType] IN ('image/jpeg', 'image/png', 'image/webp')"));
            e.HasKey(i => i.ApplicationId);
            e.Property(i => i.ApplicationId).ValueGeneratedNever();
            e.Property(i => i.ContentType).HasMaxLength(20).IsUnicode(false).IsRequired();
            e.Property(i => i.Data).IsRequired();
            e.HasOne<Domain.Entities.Application>().WithOne().HasForeignKey<ApplicationImage>(i => i.ApplicationId).OnDelete(DeleteBehavior.NoAction);
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
