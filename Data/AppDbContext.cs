using Microsoft.EntityFrameworkCore;
using OrganizationIntranet.Models;

namespace OrganizationIntranet.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<Application> Applications => Set<Application>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<UserPermission> UserPermissions => Set<UserPermission>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<UserNotification> UserNotifications => Set<UserNotification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.ToTable("User", "Sec");
            e.HasKey(x => x.UserId);
            e.Property(x => x.Username).HasMaxLength(100).IsRequired();
            e.Property(x => x.NationalId).HasMaxLength(10);
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.Property(x => x.LastName).HasMaxLength(100).IsRequired();
            e.Property(x => x.MobileNumber).HasMaxLength(20);
            e.Property(x => x.Email).HasMaxLength(200);
            e.Property(x => x.PasswordHash).HasMaxLength(500);
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
            e.Property(x => x.RoleCode).HasMaxLength(50).IsRequired();
            e.Property(x => x.IsActive).HasDefaultValue(true);
            e.Property(x => x.CreatedAt).HasColumnType("datetime").HasDefaultValueSql("GETUTCDATE()");
            e.HasIndex(x => x.RoleCode).IsUnique().HasDatabaseName("UQ_Role_Code");
        });

        modelBuilder.Entity<UserRole>(e =>
        {
            e.ToTable("UserRole", "Sec");
            e.HasKey(x => x.UserRoleId);
            e.Property(x => x.CreatedAt).HasColumnType("datetime").HasDefaultValueSql("GETUTCDATE()");
            e.HasIndex(x => new { x.UserId, x.RoleId }).IsUnique().HasDatabaseName("UQ_UserRole");

            e.HasOne(x => x.User)
                .WithMany(u => u.UserRoles)
                .HasForeignKey(x => x.UserId)
                .HasConstraintName("FK_UserRole_User");

            e.HasOne(x => x.Role)
                .WithMany(r => r.UserRoles)
                .HasForeignKey(x => x.RoleId)
                .HasConstraintName("FK_UserRole_Role");
        });

        modelBuilder.Entity<Application>(e =>
        {
            e.ToTable("Application", "App");
            e.HasKey(x => x.ApplicationId);
            e.Property(x => x.ApplicationName).HasMaxLength(150).IsRequired();
            e.Property(x => x.ApplicationCode).HasMaxLength(50).IsRequired();
            e.Property(x => x.BaseUrl).HasMaxLength(500);
            e.Property(x => x.Description).HasMaxLength(500);
            e.Property(x => x.Icon).HasMaxLength(200);
            e.Property(x => x.DisplayOrder).HasDefaultValue(0);
            e.Property(x => x.IsActive).HasDefaultValue(true);
            e.Property(x => x.CreatedAt).HasColumnType("datetime").HasDefaultValueSql("GETUTCDATE()");
            e.HasIndex(x => x.ApplicationCode).IsUnique().HasDatabaseName("UQ_Application_Code");
        });

        modelBuilder.Entity<Permission>(e =>
        {
            e.ToTable("Permission", "Sec");
            e.HasKey(x => x.PermissionId);
            e.Property(x => x.PermissionName).HasMaxLength(150).IsRequired();
            e.Property(x => x.PermissionCode).HasMaxLength(100).IsRequired();
            e.Property(x => x.Description).HasMaxLength(500);
            e.Property(x => x.IsActive).HasDefaultValue(true);
            e.Property(x => x.CreatedAt).HasColumnType("datetime").HasDefaultValueSql("GETUTCDATE()");
            e.HasIndex(x => new { x.ApplicationId, x.PermissionCode }).IsUnique().HasDatabaseName("UQ_Permission_Code");

            e.HasOne(x => x.Application)
                .WithMany(a => a.Permissions)
                .HasForeignKey(x => x.ApplicationId)
                .HasConstraintName("FK_Permission_Application");
        });

        modelBuilder.Entity<UserPermission>(e =>
        {
            e.ToTable("UserPermission", "Sec");
            e.HasKey(x => x.UserPermissionId);
            e.Property(x => x.CreatedAt).HasColumnType("datetime").HasDefaultValueSql("GETUTCDATE()");
            e.HasIndex(x => new { x.UserId, x.PermissionId }).IsUnique().HasDatabaseName("UQ_UserPermission");

            e.HasOne(x => x.User)
                .WithMany(u => u.UserPermissions)
                .HasForeignKey(x => x.UserId)
                .HasConstraintName("FK_UserPermission_User");

            e.HasOne(x => x.Permission)
                .WithMany(p => p.UserPermissions)
                .HasForeignKey(x => x.PermissionId)
                .HasConstraintName("FK_UserPermission_Permission");
        });

        modelBuilder.Entity<Notification>(e =>
        {
            e.ToTable("Notification", "Notify");
            e.HasKey(x => x.NotificationId);
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.Property(x => x.Message).IsRequired();
            e.Property(x => x.NotificationType).HasMaxLength(50);
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
            e.ToTable("UserNotification", "Notify");
            e.HasKey(x => x.UserNotificationId);
            e.Property(x => x.IsRead).HasDefaultValue(false);
            e.Property(x => x.ReadAt).HasColumnType("datetime2");
            e.Property(x => x.CreatedAt).HasColumnType("datetime").HasDefaultValueSql("GETUTCDATE()");
            e.HasIndex(x => new { x.NotificationId, x.UserId }).IsUnique().HasDatabaseName("UQ_UserNotification");

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
