using OrganizationIntranet.Application.Abstractions;
using OrganizationIntranet.Contracts;
using OrganizationIntranet.Domain.Entities;

namespace OrganizationIntranet.Application.Services;

/// <summary>
/// منطق چارت سازمانی: فهرست درختی، ساخت، ویرایش و جابه‌جایی واحد و حذف. قواعد: عنوان در هر پدر یکتا است، واحد زیرمجموعه‌ی خودش نمی‌شود،
/// عمق از <see cref="MaxDepth"/> بیشتر نمی‌شود، واحد دارای زیرمجموعه یا کاربر حذف نمی‌شود و واحد غیرفعال زیر واحد فعال جدید نمی‌رود.
/// با هر تغییر نام یا جابه‌جایی، مسیر کامل (FullPath) واحد و همه‌ی زیرمجموعه‌هایش دوباره ساخته می‌شود.
/// </summary>
public sealed class OrganizationService(IOrganizationRepository repository)
{
    /// <summary>بیشترین عمق درخت (ریشه سطح ۱)؛ جلوی حلقه‌ی بی‌پایان و درخت غیرعملی را می‌گیرد.</summary>
    public const int MaxDepth = 8;

    /// <summary>عنوان پاک‌شده؛ خالی یا بلندتر از حد مجاز یا شامل «/» (جداکننده‌ی مسیر) پذیرفته نیست.</summary>
    private static string ValidTitle(string? title)
    {
        var value = title?.Trim() ?? "";
        if (value.Length == 0 || value.Length > OrgUnit.MaxTitleLength) throw new ArgumentException($"عنوان واحد الزامی است و حداکثر {OrgUnit.MaxTitleLength} کاراکتر دارد");
        if (value.Contains(OrgUnit.PathSeparator)) throw new ArgumentException("عنوان واحد نباید شامل «/» باشد");
        return value;
    }

    /// <summary>مسیر کامل یک واحد با عنوان داده‌شده زیر پدرش.</summary>
    private static string PathOf(OrgUnit? parent, string title) => parent is null ? title : parent.FullPath + OrgUnit.PathSeparator + title;

    /// <summary>سطح یک واحد در درخت (ریشه = ۱).</summary>
    private static int LevelOf(OrgUnit unit, IReadOnlyDictionary<int, OrgUnit> all)
    {
        var level = 1; var seen = new HashSet<int> { unit.UnitId };
        for (var p = unit.ParentId; p is not null; p = all[p.Value].ParentId)
        {
            if (!seen.Add(p.Value)) throw new InvalidOperationException("چارت سازمانی حلقه دارد");
            level++;
        }
        return level;
    }

    /// <summary>ارتفاع زیردرخت یک واحد، یعنی تعداد سطح‌های خودش و فرزندانش (واحد بدون فرزند = ۱).</summary>
    private static int HeightOf(OrgUnit unit, ILookup<int?, OrgUnit> children) =>
        1 + (children[unit.UnitId].Select(c => HeightOf(c, children)).DefaultIfEmpty(0).Max());

    /// <summary>فهرست درختی: ریشه‌ها و فرزندان به ترتیب نمایش و عنوان، پدر پیش از فرزندانش؛ با شمار فرزند و کاربر مستقیم.</summary>
    public async Task<List<OrgUnitDto>> TreeAsync()
    {
        var units = await repository.GetUnitsAsync();
        var users = await repository.GetUserCountsAsync();
        var children = units.ToLookup(u => u.ParentId);
        var result = new List<OrgUnitDto>(units.Count);
        void Walk(int? parent, int depth)
        {
            foreach (var u in children[parent].OrderBy(x => x.DisplayOrder).ThenBy(x => x.Title, StringComparer.Ordinal))
            {
                result.Add(new(u.UnitId, u.ParentId, u.Title, u.FullPath, depth, u.DisplayOrder, u.IsActive, children[u.UnitId].Count(), users.GetValueOrDefault(u.UnitId)));
                Walk(u.UnitId, depth + 1);
            }
        }
        Walk(null, 0);
        return result;
    }

    /// <summary>واحد تازه زیر پدر داده‌شده (یا ریشه). عنوان باید زیر همان پدر یکتا باشد.</summary>
    public async Task<OrgUnitDto> CreateAsync(OrgUnitRequest request, long actor)
    {
        var title = ValidTitle(request.Title);
        var all = (await repository.GetUnitsAsync()).ToDictionary(u => u.UnitId);
        var parent = request.ParentId is null ? null : all.GetValueOrDefault(request.ParentId.Value) ?? throw new ArgumentException("واحد بالاتر یافت نشد");
        if (parent is not null && LevelOf(parent, all) >= MaxDepth) throw new ArgumentException($"عمق چارت سازمانی حداکثر {MaxDepth} سطح است");
        if (parent is { IsActive: false } && request.IsActive) throw new ArgumentException("زیر واحد غیرفعال نمی‌توان واحد فعال ساخت");
        EnsureTitleFree(all.Values, request.ParentId, title, null);
        var now = DateTime.UtcNow;
        var unit = new OrgUnit { ParentId = request.ParentId, Title = title, FullPath = PathOf(parent, title), DisplayOrder = request.DisplayOrder, IsActive = request.IsActive, CreatedAt = now, UpdatedAt = now, UpdatedBy = actor };
        repository.AddUnit(unit);
        await repository.SaveAsync();
        return new(unit.UnitId, unit.ParentId, unit.Title, unit.FullPath, parent is null ? 0 : LevelOf(parent, all), unit.DisplayOrder, unit.IsActive, 0, 0);
    }

    /// <summary>ویرایش عنوان، ترتیب و فعال‌بودن و (با ParentId تازه) جابه‌جایی واحد؛ مسیر زیرمجموعه‌ها دوباره ساخته می‌شود.</summary>
    public async Task UpdateAsync(int id, OrgUnitRequest request, long actor)
    {
        var title = ValidTitle(request.Title);
        var all = (await repository.GetUnitsAsync()).ToDictionary(u => u.UnitId);
        var unit = all.GetValueOrDefault(id) ?? throw new KeyNotFoundException();
        var children = all.Values.ToLookup(u => u.ParentId);
        var parent = request.ParentId is null ? null : all.GetValueOrDefault(request.ParentId.Value) ?? throw new ArgumentException("واحد بالاتر یافت نشد");
        if (request.ParentId != unit.ParentId)
        {
            if (request.ParentId == id || (parent is not null && IsDescendant(parent, id, all))) throw new ArgumentException("واحد را نمی‌توان زیر خودش یا زیرمجموعه‌اش برد");
            var baseLevel = parent is null ? 0 : LevelOf(parent, all);
            if (baseLevel + HeightOf(unit, children) > MaxDepth) throw new ArgumentException($"عمق چارت سازمانی حداکثر {MaxDepth} سطح است");
        }
        EnsureTitleFree(all.Values, request.ParentId, title, id);
        if (request.IsActive && parent is { IsActive: false }) throw new ArgumentException("زیر واحد غیرفعال نمی‌توان واحد را فعال نگه داشت");
        unit.Title = title; unit.ParentId = request.ParentId; unit.Parent = parent; unit.DisplayOrder = request.DisplayOrder; unit.IsActive = request.IsActive;
        unit.UpdatedAt = DateTime.UtcNow; unit.UpdatedBy = actor;
        Rebuild(unit, parent is null ? null : parent.FullPath, children);
        if (!unit.IsActive) Deactivate(unit, children);
        await repository.SaveAsync();
    }

    /// <summary>فعال/غیرفعال کردن واحد. غیرفعال‌کردن زیرمجموعه‌ها را هم غیرفعال می‌کند؛ فعال‌کردن فقط وقتی پدر فعال باشد.</summary>
    public async Task ToggleAsync(int id, long actor)
    {
        var all = (await repository.GetUnitsAsync()).ToDictionary(u => u.UnitId);
        var unit = all.GetValueOrDefault(id) ?? throw new KeyNotFoundException();
        if (!unit.IsActive && unit.ParentId is not null && !all[unit.ParentId.Value].IsActive) throw new ArgumentException("ابتدا واحد بالاتر را فعال کنید");
        unit.IsActive = !unit.IsActive; unit.UpdatedAt = DateTime.UtcNow; unit.UpdatedBy = actor;
        if (!unit.IsActive) Deactivate(unit, all.Values.ToLookup(u => u.ParentId));
        await repository.SaveAsync();
    }

    /// <summary>حذف واحد؛ فقط وقتی زیرمجموعه و کاربر ندارد (برای جلوگیری از گم‌شدن ساختار؛ به‌جای آن غیرفعال کنید).</summary>
    public async Task DeleteAsync(int id)
    {
        var unit = await repository.FindUnitAsync(id) ?? throw new KeyNotFoundException();
        var all = await repository.GetUnitsAsync();
        if (all.Any(u => u.ParentId == id)) throw new ArgumentException("این واحد زیرمجموعه دارد؛ ابتدا زیرمجموعه‌ها را جابه‌جا یا حذف کنید");
        if (await repository.HasUsersAsync(id)) throw new ArgumentException("کاربرانی به این واحد وصل‌اند؛ ابتدا واحد آن‌ها را عوض کنید یا واحد را غیرفعال کنید");
        repository.RemoveUnit(unit);
        await repository.SaveAsync();
    }

    /// <summary>آیا candidate در زیردرخت unitId است؟ (بالا رفتن از candidate تا ریشه)</summary>
    private static bool IsDescendant(OrgUnit candidate, int unitId, IReadOnlyDictionary<int, OrgUnit> all)
    {
        for (OrgUnit? p = candidate; p is not null; p = p.ParentId is null ? null : all[p.ParentId.Value])
            if (p.UnitId == unitId) return true;
        return false;
    }

    /// <summary>عنوان تکراری میان فرزندان همان پدر (بدون فرق حروف انگلیسی بزرگ و کوچک) پذیرفته نیست.</summary>
    private static void EnsureTitleFree(IEnumerable<OrgUnit> all, int? parentId, string title, int? exceptId)
    {
        if (all.Any(u => u.ParentId == parentId && u.UnitId != exceptId && string.Equals(u.Title, title, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("در این واحد بالاتر، واحدی با همین عنوان وجود دارد");
    }

    /// <summary>مسیر کامل واحد و همه‌ی زیرمجموعه‌هایش را از مسیر پدر دوباره می‌سازد.</summary>
    private static void Rebuild(OrgUnit unit, string? parentPath, ILookup<int?, OrgUnit> children)
    {
        unit.FullPath = parentPath is null ? unit.Title : parentPath + OrgUnit.PathSeparator + unit.Title;
        if (unit.FullPath.Length > OrgUnit.MaxPathLength) throw new ArgumentException("مسیر کامل واحد از حد مجاز بلندتر می‌شود؛ عنوان‌ها را کوتاه‌تر کنید");
        foreach (var child in children[unit.UnitId]) Rebuild(child, unit.FullPath, children);
    }

    /// <summary>زیرمجموعه‌های یک واحد غیرفعال را هم غیرفعال می‌کند.</summary>
    private static void Deactivate(OrgUnit unit, ILookup<int?, OrgUnit> children)
    {
        foreach (var child in children[unit.UnitId]) { child.IsActive = false; child.UpdatedAt = unit.UpdatedAt; child.UpdatedBy = unit.UpdatedBy; Deactivate(child, children); }
    }
}
