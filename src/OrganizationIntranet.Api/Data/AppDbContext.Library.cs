using Microsoft.EntityFrameworkCore;
using OrganizationIntranet.Domain.Entities;

namespace OrganizationIntranet.Api.Data;

/// <summary>بخش کتابخانه‌ی اسناد از DbContext: دسته‌بندی‌ها و سندها در Schema تازه‌ی Lib؛ migration 017_LibraryDocuments.</summary>
public partial class AppDbContext
{
    public DbSet<LibraryCategory> LibraryCategories => Set<LibraryCategory>();
    public DbSet<LibraryDocument> LibraryDocuments => Set<LibraryDocument>();

    private void ConfigureLibrary(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LibraryCategory>(e =>
        {
            e.ToTable("Category", "Lib", t =>
            {
                t.HasCheckConstraint("CK_LibCategory_Group", "[GroupCode] IN ('rules','publications','forms')");
                if (Database.IsSqlServer()) t.HasCheckConstraint("CK_LibCategory_Title", "LEN(LTRIM(RTRIM([Title]))) > 0");
            });
            e.HasKey(x => x.CategoryId);
            e.Property(x => x.GroupCode).HasMaxLength(20).IsUnicode(false).IsRequired();
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.Property(x => x.IsActive).HasDefaultValue(true);
            e.Property(x => x.DisplayOrder).HasDefaultValue(0);
            e.HasIndex(x => new { x.GroupCode, x.Title }).IsUnique().HasDatabaseName("UX_LibCategory_Group_Title");
        });
        modelBuilder.Entity<LibraryDocument>(e =>
        {
            e.ToTable("Document", "Lib", t =>
            {
                t.HasCheckConstraint("CK_LibDocument_Size", "[SizeBytes] > 0");
                if (Database.IsSqlServer()) t.HasCheckConstraint("CK_LibDocument_Title", "LEN(LTRIM(RTRIM([Title]))) > 0");
            });
            e.HasKey(x => x.DocumentId);
            e.Property(x => x.Title).HasMaxLength(300).IsRequired();
            e.Property(x => x.Description).HasMaxLength(1000);
            e.Property(x => x.FileName).HasMaxLength(260).IsRequired();
            e.Property(x => x.ContentType).HasMaxLength(100).IsUnicode(false).IsRequired();
            e.Property(x => x.StoredName).HasMaxLength(32).IsFixedLength().IsUnicode(false).IsRequired();
            e.Property(x => x.Sha256).HasMaxLength(32).IsFixedLength().IsRequired();
            e.Property(x => x.IsActive).HasDefaultValue(true);
            e.HasIndex(x => x.StoredName).IsUnique().HasDatabaseName("UX_LibDocument_StoredName");
            e.HasIndex(x => new { x.CategoryId, x.IsActive }).HasDatabaseName("IX_LibDocument_Category");
            e.HasIndex(x => x.CreatedBy).HasDatabaseName("IX_LibDocument_CreatedBy");
            e.HasIndex(x => x.UpdatedBy).HasDatabaseName("IX_LibDocument_UpdatedBy");
            e.HasOne(x => x.Category).WithMany(c => c.Documents).HasForeignKey(x => x.CategoryId).HasConstraintName("FK_LibDocument_Category").OnDelete(DeleteBehavior.NoAction);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedBy).HasConstraintName("FK_LibDocument_CreatedBy").OnDelete(DeleteBehavior.NoAction);
            e.HasOne(x => x.UpdatedByUser).WithMany().HasForeignKey(x => x.UpdatedBy).HasConstraintName("FK_LibDocument_UpdatedBy").OnDelete(DeleteBehavior.NoAction);
        });
    }
}
