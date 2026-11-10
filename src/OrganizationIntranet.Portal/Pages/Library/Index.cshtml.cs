using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OrganizationIntranet.Contracts;
using OrganizationIntranet.UI;

namespace OrganizationIntranet.Pages.Library;

/// <summary>
/// قوانین و مقررات، انتشارات و فرم‌های سازمانی برای همه (بدون ورود): تب بخش، دسته، جست‌وجو و دانلود. داده از API (مدیریت در پنل) می‌آید؛
/// بدون query فقط دسته‌ها نمایش داده می‌شوند و با بخش، دسته یا جست‌وجو فهرست سندها. فیلتر با query string است و جاوااسکریپت نمی‌خواهد.
/// </summary>
[AllowAnonymous]
public class IndexModel(ApiClient api) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? Group { get; set; }
    [BindProperty(SupportsGet = true)] public int? Category { get; set; }
    [BindProperty(SupportsGet = true)] public string? Q { get; set; }

    /// <summary>همه‌ی سندهای منطبق با جست‌وجو (پیش از فیلتر بخش و دسته)؛ شمار تب‌ها از همین می‌آید.</summary>
    private LibraryCatalogDto catalog = new([], [], 0);
    public bool HasAnyContent { get; private set; }
    public bool ApiUnavailable { get; private set; }
    /// <summary>بدون هیچ فیلتری فقط دسته‌ها نشان داده می‌شود، نه سندها.</summary>
    public bool IsOverview => Group is null && Category is null && Q is null;
    public IReadOnlyList<LibraryCategoryDto> Categories => catalog.Categories;
    public int CountAll => catalog.Total;
    public int Count(string group) => catalog.Categories.Where(c => c.GroupCode == group).Sum(c => c.DocumentCount);

    /// <summary>سندهای فیلترشده‌ی بخش و دسته، به ترتیب بخش و دسته.</summary>
    public IReadOnlyList<LibraryDocumentDto> Documents { get; private set; } = [];
    public IEnumerable<IGrouping<string, LibraryDocumentDto>> DocumentsByGroup => Documents.GroupBy(d => d.GroupCode);

    public async Task OnGetAsync()
    {
        Q = string.IsNullOrWhiteSpace(Q) ? null : Q.Trim()[..Math.Min(Q.Trim().Length, LibraryRules.MaxSearchLength)];
        if (!LibraryGroups.IsValid(Group)) Group = null;
        try
        {
            catalog = await api.GetAsync<LibraryCatalogDto>("api/public/library?documents=true" + (Q is null ? "" : "&q=" + Uri.EscapeDataString(Q)));
        }
        catch (ApiException) { ApiUnavailable = true; return; }
        if (Category is { } id && catalog.Categories.All(c => c.CategoryId != id)) Category = null;
        HasAnyContent = catalog.Total > 0;
        Documents = catalog.Documents.Where(d => (Group is null || d.GroupCode == Group) && (Category is null || d.CategoryId == Category)).ToList();
    }
}
