using Microsoft.EntityFrameworkCore;
using OrganizationIntranet.Domain.Entities;

namespace OrganizationIntranet.Api.Data;

public partial class AppDbContext
{
    private void ConfigureAuthenticationIntegrity(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AuthenticationSettings>().ToTable("AuthenticationSettings", "Sec", table =>
        {
            table.HasCheckConstraint("CK_AuthenticationSettings_Singleton", "[Id] = 1");
            // SQLite test databases do not implement SQL Server ISJSON/DATALENGTH.
            if (Database.IsSqlServer())
                table.HasCheckConstraint("CK_AuthenticationSettings_Json", "ISJSON([Json]) = 1 AND DATALENGTH([Json]) <= 20000");
        });
        modelBuilder.Entity<AuthenticationChallenge>().ToTable("AuthenticationChallenge", "Sec", table =>
        {
            table.HasCheckConstraint("CK_AuthenticationChallenge_Attempts", "[Attempts] >= 0");
            // 'otp-login' و 'national-id-sms' با migration 006_NationalIdLogin اضافه شدند.
            table.HasCheckConstraint("CK_AuthenticationChallenge_Purpose", "[Purpose] IN ('login', 'reset', 'enroll', 'otp-login')");
            table.HasCheckConstraint("CK_AuthenticationChallenge_LoginMethod", "[LoginMethod] IN ('password', 'ldap', 'national-id-sms')");
        });
        modelBuilder.Entity<User>().ToTable("User", "Sec", table =>
            table.HasCheckConstraint("CK_User_AuthenticationCounters", "[FailedLoginAttempts] >= 0 AND [LastAuthenticatorStep] >= -1"));
        // یکتا نیست: کد ملیِ مشترک بین چند کاربر، ورود با کد ملی را مسدود می‌کند (fail closed).
        modelBuilder.Entity<User>().HasIndex(u => u.NationalId).HasDatabaseName("IX_User_NationalId").HasFilter("[NationalId] IS NOT NULL");
    }
}
