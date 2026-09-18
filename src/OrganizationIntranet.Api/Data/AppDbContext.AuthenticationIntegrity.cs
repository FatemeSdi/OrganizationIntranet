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
            table.HasCheckConstraint("CK_AuthenticationChallenge_Purpose", "[Purpose] IN ('login', 'reset', 'enroll')");
            table.HasCheckConstraint("CK_AuthenticationChallenge_LoginMethod", "[LoginMethod] IN ('password', 'ldap')");
        });
        modelBuilder.Entity<User>().ToTable("User", "Sec", table =>
            table.HasCheckConstraint("CK_User_AuthenticationCounters", "[FailedLoginAttempts] >= 0 AND [LastAuthenticatorStep] >= -1"));
    }
}
