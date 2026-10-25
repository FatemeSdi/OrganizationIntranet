using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace OrganizationIntranet.Pages.Library;

/// <summary>قوانین و مقررات، انتشارات و فرم‌های سازمانی: تب بخش، جست‌وجو و نتیجه به تفکیک بخش؛ با query string و بدون نیاز به JavaScript.</summary>
[AllowAnonymous]
public class IndexModel : PageModel
{
    [BindProperty(SupportsGet = true)] public string? Group { get; set; }
    [BindProperty(SupportsGet = true)] public string? Q { get; set; }

    /// <summary>بخش‌های قابل نمایش (همه یا بخش انتخاب‌شده) با مواردِ منطبق بر جست‌وجو.</summary>
    public IReadOnlyList<(LibraryGroup Group, IReadOnlyList<LibraryItem> Items)> Results { get; private set; } = [];
    public int Total => Results.Sum(r => r.Items.Count);
    /// <summary>تب «همه» بدون جست‌وجو فقط پیش‌نمایش هر بخش را نشان می‌دهد تا با بخش‌های بزرگ شلوغ نشود؛ بقیه در تب همان بخش.</summary>
    public const int PreviewLimit = 6;
    public bool IsPreview => Group is null && Q is null;
    public IEnumerable<LibraryItem> Visible(IReadOnlyList<LibraryItem> items) => IsPreview ? items.Take(PreviewLimit) : items;
    public int Count(LibraryGroup group) => group.Items.Count(i => LibraryCatalog.Matches(i, Q));
    public int CountAll => LibraryCatalog.Groups.Sum(Count);

    public void OnGet()
    {
        if (LibraryCatalog.Find(Group) is null) Group = null;
        Q = string.IsNullOrWhiteSpace(Q) ? null : Q.Trim()[..Math.Min(Q.Trim().Length, 100)];
        Results = LibraryCatalog.Groups.Where(g => Group is null || g.Id == Group)
            .Select(g => (g, (IReadOnlyList<LibraryItem>)g.Items.Where(i => LibraryCatalog.Matches(i, Q)).ToList()))
            .Where(r => r.Item2.Count > 0).ToList();
    }
}
