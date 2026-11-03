using Microsoft.EntityFrameworkCore;
using OrganizationIntranet.Domain.Entities;

namespace OrganizationIntranet.Api.Data;

/// <summary>بخش ورود یکپارچه از DbContext: بلیت‌های یک‌بارمصرف و نگاشت نام کاربری کاربر در هر سامانه.</summary>
public partial class AppDbContext
{
    public DbSet<SsoTicket> SsoTickets => Set<SsoTicket>();
    public DbSet<UserApplicationAccount> UserApplicationAccounts => Set<UserApplicationAccount>();

    private void ConfigureSso(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SsoTicket>(e =>
        {
            e.ToTable("SsoTicket", "Sec", t => t.HasCheckConstraint("CK_SsoTicket_Lifetime", "[ExpiresAt] > [CreatedAt]"));
            e.HasKey(x => x.SsoTicketId);
            e.Property(x => x.TicketHash).HasMaxLength(32).IsFixedLength().IsRequired();
            e.HasIndex(x => x.TicketHash).IsUnique();
            e.HasIndex(x => x.ExpiresAt);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
            e.HasOne(x => x.Application).WithMany().HasForeignKey(x => x.ApplicationId).OnDelete(DeleteBehavior.NoAction);
        });
        // نام کاربری حساب کاربر در هر سامانه؛ migration 011_UserApplicationAccounts.
        modelBuilder.Entity<Domain.Entities.Application>(e =>
        {
            // تنظیمات SSO هر سامانه؛ migration 013_ApplicationSsoSettings. کلید فقط به‌صورت هش (SHA-256) و یکتا میان سامانه‌ها.
            e.ToTable("Application", "App", t => { if (Database.IsSqlServer()) t.HasCheckConstraint("CK_Application_SsoPath", "[SsoPath] IS NULL OR LEN(LTRIM(RTRIM([SsoPath]))) > 0"); });
            e.Property(a => a.SsoPath).HasMaxLength(200);
            e.Property(a => a.SsoKeyHash).HasMaxLength(32).IsFixedLength();
            e.HasIndex(a => a.SsoKeyHash).IsUnique().HasFilter("[SsoKeyHash] IS NOT NULL").HasDatabaseName("UX_Application_SsoKeyHash");
        });
        modelBuilder.Entity<UserApplicationAccount>(e =>
        {
            e.ToTable("UserApplicationAccount", "App", t => { if (Database.IsSqlServer()) t.HasCheckConstraint("CK_UserApplicationAccount_Name", "LEN(LTRIM(RTRIM([ExternalUsername]))) > 0"); });
            e.HasKey(x => x.UserApplicationAccountId);
            e.Property(x => x.ExternalUsername).HasMaxLength(100).IsRequired();
            e.HasIndex(x => new { x.UserId, x.ApplicationId }).IsUnique().HasDatabaseName("UX_UserApplicationAccount_User_Application");
            e.HasIndex(x => new { x.ApplicationId, x.ExternalUsername }).IsUnique().HasDatabaseName("UX_UserApplicationAccount_Application_Name");
            e.HasIndex(x => x.UpdatedBy);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
            e.HasOne(x => x.Application).WithMany().HasForeignKey(x => x.ApplicationId).OnDelete(DeleteBehavior.NoAction);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UpdatedBy).OnDelete(DeleteBehavior.NoAction);
        });
    }
}
