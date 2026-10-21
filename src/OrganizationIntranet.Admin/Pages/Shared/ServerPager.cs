namespace OrganizationIntranet.Pages.Shared;

/// <summary>مدل partial «_ServerPager»: صفحه‌بندی فهرست‌هایی که API آن‌ها صفحه‌به‌صفحه برمی‌گرداند. RouteValues فیلترهای فعلی را در پیوندها نگه می‌دارد.</summary>
public sealed record ServerPager(int Page, int PageSize, int Total, string Noun, IDictionary<string, string?>? RouteValues = null)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(Total / (double)Math.Max(1, PageSize)));

    /// <summary>شماره‌های قابل نمایش: اول، آخر و دو صفحه‌ی اطراف صفحه‌ی جاری؛ 0 یعنی «…».</summary>
    public IEnumerable<int> Numbers()
    {
        var shown = new SortedSet<int> { 1, TotalPages };
        for (var p = Page - 2; p <= Page + 2; p++) if (p >= 1 && p <= TotalPages) shown.Add(p);
        var previous = 0;
        foreach (var p in shown)
        {
            if (previous != 0 && p - previous > 1) yield return 0;
            yield return p;
            previous = p;
        }
    }

    public Dictionary<string, string?> RouteFor(int page)
    {
        var values = new Dictionary<string, string?>(RouteValues ?? new Dictionary<string, string?>()) { ["pageNumber"] = page.ToString() };
        return values;
    }
}
