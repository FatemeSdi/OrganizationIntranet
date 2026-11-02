using Microsoft.EntityFrameworkCore;
using OrganizationIntranet.Domain.Entities;

namespace OrganizationIntranet.Api.Data;

/// <summary>بخش چارت سازمانی از DbContext: درخت واحدها (Org.Unit) و پیوند اختیاری کاربر به واحد؛ migration 012_OrganizationChart.</summary>
public partial class AppDbContext
{
    public DbSet<OrgUnit> OrgUnits => Set<OrgUnit>();

    private void ConfigureOrganization(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OrgUnit>(e =>
        {
            e.ToTable("Unit", "Org", t =>
            {
                t.HasCheckConstraint("CK_OrgUnit_NotOwnParent", "[ParentId] IS NULL OR [ParentId] <> [UnitId]");
                if (Database.IsSqlServer()) t.HasCheckConstraint("CK_OrgUnit_Title", "LEN(LTRIM(RTRIM([Title]))) > 0");
            });
            e.HasKey(x => x.UnitId);
            e.Property(x => x.Title).HasMaxLength(OrgUnit.MaxTitleLength).IsRequired();
            e.Property(x => x.FullPath).HasMaxLength(OrgUnit.MaxPathLength).IsRequired();
            e.HasIndex(x => new { x.ParentId, x.Title }).IsUnique().HasDatabaseName("UX_OrgUnit_Parent_Title");
            e.HasIndex(x => x.SourceId).IsUnique().HasFilter("[SourceId] IS NOT NULL").HasDatabaseName("UX_OrgUnit_SourceId");
            e.HasIndex(x => x.UpdatedBy);
            e.HasOne(x => x.Parent).WithMany(x => x.Children).HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.NoAction);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UpdatedBy).OnDelete(DeleteBehavior.NoAction);
        });
        modelBuilder.Entity<User>(e =>
        {
            e.HasIndex(x => x.UnitId).HasDatabaseName("IX_User_UnitId");
            e.HasOne(x => x.Unit).WithMany(x => x.Users).HasForeignKey(x => x.UnitId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_User_OrgUnit");
        });
    }
}
