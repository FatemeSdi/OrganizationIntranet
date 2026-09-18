using Microsoft.EntityFrameworkCore;
using OrganizationIntranet.Domain.Entities;

namespace OrganizationIntranet.Api.Data;

public partial class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Publication> Publications => Set<Publication>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<Domain.Entities.Application> Applications => Set<Domain.Entities.Application>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<UserPermission> UserPermissions => Set<UserPermission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<UserNotification> UserNotifications => Set<UserNotification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureAuthenticationIntegrity(modelBuilder);
        modelBuilder.Entity<AuthenticationSettings>(e =>
        {
            e.ToTable("AuthenticationSettings", "Sec");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Json).HasMaxLength(10000);
            e.Property(x => x.ProtectedSmsApiKey).HasMaxLength(4000);
            e.Property(x => x.Revision).IsConcurrencyToken();
        });
        modelBuilder.Entity<AuthenticationChallenge>(e =>
        {
            e.ToTable("AuthenticationChallenge", "Sec");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasMaxLength(64).IsUnicode(false);
            e.Property(x => x.Purpose).HasMaxLength(20).IsUnicode(false);
            e.Property(x => x.LoginMethod).HasMaxLength(20).IsUnicode(false);
            e.Property(x => x.SecurityStamp).HasMaxLength(32).IsUnicode(false);
            e.Property(x => x.CodeHash).HasMaxLength(64).IsUnicode(false);
            e.Property(x => x.ProtectedSecret).HasMaxLength(2000);
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasIndex(x => x.ExpiresAt);
            e.HasIndex(x => new { x.UserId, x.Purpose });
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
        });
        modelBuilder.Entity<Publication>(e =>
        {
            e.ToTable("Publication", "Notify", t =>
            {
                t.HasCheckConstraint("CK_Publication_Kind", "[Kind] IN ('News', 'Announcement', 'Circular')");
                t.HasCheckConstraint("CK_Publication_PublishState", "([IsPublished] = 0 AND [PublishedAt] IS NULL) OR ([IsPublished] = 1 AND [PublishedAt] IS NOT NULL)");
            });
            e.HasKey(p => p.PublicationId);
            e.Property(p => p.Title).HasMaxLength(200).IsRequired();
            e.Property(p => p.Summary).HasMaxLength(1000).IsRequired();
            e.Property(p => p.Body).HasMaxLength(50000).IsRequired();
            e.Property(p => p.Kind).HasMaxLength(20).IsRequired();
            e.Property(p => p.Revision).IsConcurrencyToken();
            e.HasIndex(p => new { p.IsPublished, p.PublishedAt, p.PublicationId });
            e.HasIndex(p => p.Kind);
            e.HasOne<User>().WithMany().HasForeignKey(p => p.CreatedBy).OnDelete(DeleteBehavior.NoAction);
            e.HasOne<User>().WithMany().HasForeignKey(p => p.UpdatedBy).OnDelete(DeleteBehavior.NoAction);
        });
        modelBuilder.Entity<User>(e =>
        {
            e.ToTable("User", "Sec");
            e.HasKey(x => x.UserId);
            e.Property(x => x.Username).HasMaxLength(100).IsRequired();
            e.Property(x => x.NationalId).HasMaxLength(10).IsUnicode(false);
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.Property(x => x.LastName).HasMaxLength(100).IsRequired();
            e.Property(x => x.MobileNumber).HasMaxLength(20).IsUnicode(false);
            e.Property(x => x.Email).HasMaxLength(200);
            e.Property(x => x.PasswordHash).HasMaxLength(500);
            e.Property(x => x.SecurityStamp).HasMaxLength(32).IsUnicode(false);
            e.Property(x => x.AuthenticatorSecret).HasMaxLength(2000);
            e.Property(x => x.AuthenticationVersion).IsConcurrencyToken();
            e.Property(x => x.IsActive).HasDefaultValue(true);
            e.Property(x => x.CreatedAt).HasColumnType("datetime").HasDefaultValueSql("GETUTCDATE()");
            e.Property(x => x.LastLogin).HasColumnType("datetime");
            e.HasIndex(x => x.Username).IsUnique().HasDatabaseName("UQ_User_Username");
        });

        modelBuilder.Entity<Role>(e =>
        {
            e.ToTable("Role", "Sec");
            e.HasKey(x => x.RoleId);
            e.Property(x => x.RoleName).HasMaxLength(100).IsRequired();
            e.Property(x => x.RoleCode).HasMaxLength(50).IsUnicode(false).IsRequired();
            e.Property(x => x.IsActive).HasDefaultValue(true);
            e.Property(x => x.CreatedAt).HasColumnType("datetime").HasDefaultValueSql("GETUTCDATE()");
            e.HasIndex(x => x.RoleCode).IsUnique().HasDatabaseName("UQ_Role_RoleCode");
        });

        modelBuilder.Entity<UserRole>(e =>
        {
            e.ToTable("UserRole", "Sec");
            e.HasKey(x => x.UserRoleId);
            e.Property(x => x.CreatedAt).HasColumnType("datetime").HasDefaultValueSql("GETUTCDATE()");
            e.HasIndex(x => new { x.UserId, x.RoleId }).IsUnique().HasDatabaseName("UQ_UserRole_UserId_RoleId");

            e.HasOne(x => x.User)
                .WithMany(u => u.UserRoles)
                .HasForeignKey(x => x.UserId)
                .HasConstraintName("FK_UserRole_User")
                .OnDelete(DeleteBehavior.NoAction);

            e.HasOne(x => x.Role)
                .WithMany(r => r.UserRoles)
                .HasForeignKey(x => x.RoleId)
                .HasConstraintName("FK_UserRole_Role")
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<Domain.Entities.Application>(e =>
        {
            e.ToTable("Application", "App");
            e.HasKey(x => x.ApplicationId);
            e.Property(x => x.ApplicationName).HasMaxLength(150).IsRequired();
            e.Property(x => x.ApplicationCode).HasMaxLength(50).IsUnicode(false).IsRequired();
            e.Property(x => x.BaseUrl).HasMaxLength(500);
            e.Property(x => x.Description).HasMaxLength(500);
            e.Property(x => x.Icon).HasMaxLength(200);
            e.Property(x => x.DisplayOrder).HasDefaultValue(0);
            e.Property(x => x.IsActive).HasDefaultValue(true);
            e.Property(x => x.CreatedAt).HasColumnType("datetime").HasDefaultValueSql("GETUTCDATE()");
            e.HasIndex(x => x.ApplicationCode).IsUnique().HasDatabaseName("UQ_Application_ApplicationCode");
            e.HasIndex(x => x.DisplayOrder).HasDatabaseName("IX_Application_DisplayOrder");
        });

        modelBuilder.Entity<Permission>(e =>
        {
            e.ToTable("Permission", "Sec");
            e.HasKey(x => x.PermissionId);
            e.Property(x => x.PermissionName).HasMaxLength(150).IsRequired();
            e.Property(x => x.PermissionCode).HasMaxLength(100).IsUnicode(false).IsRequired();
            e.Property(x => x.Description).HasMaxLength(500);
            e.Property(x => x.IsActive).HasDefaultValue(true);
            e.Property(x => x.CreatedAt).HasColumnType("datetime").HasDefaultValueSql("GETUTCDATE()");
            e.HasIndex(x => new { x.ApplicationId, x.PermissionCode }).IsUnique().HasDatabaseName("UQ_Permission_ApplicationId_PermissionCode");
            // Claims currently identify permissions by code alone, so preserve the live database's global uniqueness.
            e.HasIndex(x => x.PermissionCode).IsUnique().HasDatabaseName("UX_Permission_PermissionCode");

            e.HasOne(x => x.Application)
                .WithMany(a => a.Permissions)
                .HasForeignKey(x => x.ApplicationId)
                .HasConstraintName("FK_Permission_Application")
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<UserPermission>(e =>
        {
            e.ToTable("UserPermission", "Sec");
            e.HasKey(x => x.UserPermissionId);
            e.Property(x => x.CreatedAt).HasColumnType("datetime").HasDefaultValueSql("GETUTCDATE()");
            e.HasIndex(x => new { x.UserId, x.PermissionId }).IsUnique().HasDatabaseName("UQ_UserPermission_UserId_PermissionId");

            e.HasOne(x => x.User)
                .WithMany(u => u.UserPermissions)
                .HasForeignKey(x => x.UserId)
                .HasConstraintName("FK_UserPermission_User")
                .OnDelete(DeleteBehavior.NoAction);

            e.HasOne(x => x.Permission)
                .WithMany(p => p.UserPermissions)
                .HasForeignKey(x => x.PermissionId)
                .HasConstraintName("FK_UserPermission_Permission")
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<RolePermission>(e =>
        {
            e.ToTable("RolePermission", "Sec");
            e.HasKey(x => x.RolePermissionId);
            // Existing timestamps are retained; new assignments consistently use UTC.
            e.Property(x => x.CreatedAt).HasColumnType("datetime2").HasDefaultValueSql("GETUTCDATE()");
            e.HasIndex(x => new { x.RoleId, x.PermissionId }).IsUnique().HasDatabaseName("UX_RolePermission_RoleId_PermissionId");

            // در اسکریپت SQL این دو FK بدون ON DELETE CASCADE ساخته شدند (پیش‌فرض SQL Server یعنی NO ACTION)،
            // پس اینجا هم صریحاً NoAction تنظیم می‌شود تا با رفتار واقعی دیتابیس یکی باشد.
            e.HasOne(x => x.Role)
                .WithMany(r => r.RolePermissions)
                .HasForeignKey(x => x.RoleId)
                .HasConstraintName("FK_RolePermission_Role")
                .OnDelete(DeleteBehavior.NoAction);

            e.HasOne(x => x.Permission)
                .WithMany(p => p.RolePermissions)
                .HasForeignKey(x => x.PermissionId)
                .HasConstraintName("FK_RolePermission_Permission")
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<Notification>(e =>
        {
            e.ToTable("Notification", "Notify");
            e.HasKey(x => x.NotificationId);
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.Property(x => x.Message).IsRequired();
            e.Property(x => x.NotificationType).HasMaxLength(50).IsUnicode(false);
            e.Property(x => x.TargetUrl).HasMaxLength(500);
            e.Property(x => x.CreatedAt).HasColumnType("datetime").HasDefaultValueSql("GETUTCDATE()");

            e.HasOne(x => x.CreatedByUser)
                .WithMany(u => u.CreatedNotifications)
                .HasForeignKey(x => x.CreatedBy)
                .HasConstraintName("FK_Notification_CreatedBy")
                .OnDelete(DeleteBehavior.NoAction);

            e.HasOne(x => x.Application)
                .WithMany(a => a.Notifications)
                .HasForeignKey(x => x.ApplicationId)
                .HasConstraintName("FK_Notification_Application")
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<UserNotification>(e =>
        {
            e.ToTable("UserNotification", "Notify", t => t.HasCheckConstraint(
                "CK_UserNotification_ReadState", "([IsRead] = 0 AND [ReadAt] IS NULL) OR ([IsRead] = 1 AND [ReadAt] IS NOT NULL)"));
            e.HasKey(x => x.UserNotificationId);
            e.Property(x => x.IsRead).HasDefaultValue(false);
            e.Property(x => x.ReadAt).HasColumnType("datetime");
            e.Property(x => x.CreatedAt).HasColumnType("datetime").HasDefaultValueSql("GETUTCDATE()");
            e.HasIndex(x => new { x.NotificationId, x.UserId }).IsUnique().HasDatabaseName("UQ_UserNotification_NotificationId_UserId");
            e.HasIndex(x => new { x.UserId, x.IsRead, x.CreatedAt }).HasDatabaseName("IX_UserNotification_UserId_IsRead_CreatedAt");

            e.HasOne(x => x.Notification)
                .WithMany(n => n.UserNotifications)
                .HasForeignKey(x => x.NotificationId)
                .HasConstraintName("FK_UserNotification_Notification")
                .OnDelete(DeleteBehavior.NoAction);

            e.HasOne(x => x.User)
                .WithMany(u => u.UserNotifications)
                .HasForeignKey(x => x.UserId)
                .HasConstraintName("FK_UserNotification_User")
                .OnDelete(DeleteBehavior.NoAction);
        });
    }
}
