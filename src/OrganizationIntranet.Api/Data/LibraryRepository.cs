using Microsoft.EntityFrameworkCore;
using OrganizationIntranet.Application.Abstractions;
using OrganizationIntranet.Domain.Entities;

namespace OrganizationIntranet.Api.Data;

/// <summary>پیاده‌سازی EF Core داده‌ی کتابخانه‌ی اسناد (Lib.Category و Lib.Document). نمایش عمومی فقط سند فعالِ دسته‌ی فعال را می‌بیند.</summary>
public sealed class LibraryRepository(AppDbContext db) : ILibraryRepository
{
    private IQueryable<LibraryDocument> PublicDocuments => db.LibraryDocuments.Include(d => d.Category).Where(d => d.IsActive && d.Category.IsActive);
    public async Task<List<LibraryCategoryRow>> PublicCategoriesAsync() => (await db.LibraryCategories.AsNoTracking().Where(c => c.IsActive)
        .Select(c => new { Category = c, Count = c.Documents.Count(d => d.IsActive) }).Where(x => x.Count > 0).ToListAsync()).Select(x => new LibraryCategoryRow(x.Category, x.Count)).ToList();
    public Task<List<LibraryDocument>> PublicDocumentsAsync() => PublicDocuments.AsNoTracking().ToListAsync();
    public Task<LibraryDocument?> FindPublicDocumentAsync(long id) => PublicDocuments.AsNoTracking().FirstOrDefaultAsync(d => d.DocumentId == id);
    public async Task<List<LibraryCategoryRow>> AllCategoriesAsync() => (await db.LibraryCategories.AsNoTracking()
        .Select(c => new { Category = c, Count = c.Documents.Count() }).ToListAsync()).Select(x => new LibraryCategoryRow(x.Category, x.Count)).ToList();
    public Task<LibraryCategory?> FindCategoryAsync(int id) => db.LibraryCategories.FirstOrDefaultAsync(c => c.CategoryId == id);
    public Task<bool> CategoryTitleTakenAsync(string groupCode, string title, int? exceptId) =>
        db.LibraryCategories.AnyAsync(c => c.GroupCode == groupCode && c.Title == title && c.CategoryId != exceptId);
    public Task<int> DocumentCountAsync(int categoryId) => db.LibraryDocuments.CountAsync(d => d.CategoryId == categoryId);
    public void AddCategory(LibraryCategory category) => db.LibraryCategories.Add(category);
    public void RemoveCategory(LibraryCategory category) => db.LibraryCategories.Remove(category);
    public async Task<(List<LibraryDocument> Items, int Total)> PageAsync(string? groupCode, int? categoryId, string? status, string? search, int page, int pageSize)
    {
        var query = db.LibraryDocuments.AsNoTracking().Include(d => d.Category).Include(d => d.UpdatedByUser).AsQueryable();
        if (groupCode is not null) query = query.Where(d => d.Category.GroupCode == groupCode);
        if (categoryId is not null) query = query.Where(d => d.CategoryId == categoryId);
        if (status == "active") query = query.Where(d => d.IsActive);
        else if (status == "inactive") query = query.Where(d => !d.IsActive);
        if (!string.IsNullOrEmpty(search)) query = query.Where(d => d.Title.Contains(search) || d.FileName.Contains(search) || (d.Description != null && d.Description.Contains(search)));
        var total = await query.CountAsync();
        return (await query.OrderByDescending(d => d.UpdatedAt).ThenByDescending(d => d.DocumentId).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(), total);
    }
    public Task<LibraryDocument?> FindDocumentAsync(long id) => db.LibraryDocuments.Include(d => d.Category).Include(d => d.UpdatedByUser).FirstOrDefaultAsync(d => d.DocumentId == id);
    public void AddDocument(LibraryDocument document) => db.LibraryDocuments.Add(document);
    public void RemoveDocument(LibraryDocument document) => db.LibraryDocuments.Remove(document);
    public Task SaveAsync() => db.SaveChangesAsync();
}
