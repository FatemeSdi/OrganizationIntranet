using OrganizationIntranet.Application.Abstractions;
using OrganizationIntranet.Application.Catalogs;
using OrganizationIntranet.Contracts;
using OrganizationIntranet.Domain.Entities;
using AppEntity = OrganizationIntranet.Domain.Entities.Application;

namespace OrganizationIntranet.Application.Services;

public sealed class OrganizationCatalogImportService(IApplicationCatalogRepository catalog, IIntranetRepository identities)
{
    private const string Username = "fatemeh.sadeghi", RoleCode = "EXPERT";
    private async Task<(User User, Role Role)> PrincipalsAsync()
    {
        var user = await identities.FindUserAsync(Username);
        var role = (await identities.GetRolesAsync()).SingleOrDefault(r => r.RoleCode == RoleCode && r.IsActive);
        if (user is not { IsActive: true } || role is null) throw new ArgumentException("حساب fatemeh.sadeghi یا نقش فعال EXPERT موجود نیست");
        return (user, role);
    }
    private static AppEntity? Existing(List<AppEntity> apps, OrganizationApplicationDefinition item)
    {
        var matches = apps.Where(a => string.Equals(a.ApplicationCode, item.Code, StringComparison.OrdinalIgnoreCase) || a.ApplicationName == item.Name).ToList();
        if (matches.Count > 1) throw new ArgumentException($"رکوردهای هم‌نام یا هم‌کد برای {item.Name} باید ابتدا در پنل اصلاح شوند");
        return matches.SingleOrDefault();
    }
    public async Task<CatalogImportPreviewDto> PreviewAsync()
    {
        var principals = await PrincipalsAsync(); var apps = await catalog.ListAsync();
        return new(OrganizationApplicationCatalog.Items.Select(item => new CatalogImportItemDto(item.Code, item.Name, item.Description,
            item.Code switch { "GATEWAY" => "عمومی", "ATTENDANCE" => $"نقش {principals.Role.RoleName}", "PAYSLIP" => $"کاربر {Username}", _ => "بدون تخصیص اولیه" }, Existing(apps, item) is not null)).ToList(), Username, principals.Role.RoleName);
    }
    public async Task<CatalogImportResultDto> ImportAsync()
    {
        var principals = await PrincipalsAsync(); var apps = await catalog.ListAsync(); var created = 0;
        // Validate all matches before attaching changes. Save once for the entire import and its grants.
        var plan = OrganizationApplicationCatalog.Items.Select((item, index) => (Item: item, Existing: Existing(apps, item), Order: index + 1)).ToList();
        foreach (var row in plan)
        {
            var app = row.Existing is null ? new AppEntity { ApplicationCode = row.Item.Code, ApplicationName = row.Item.Name,
                Description = row.Item.Description, Icon = row.Item.Icon, DisplayOrder = row.Order, CreatedAt = DateTime.UtcNow }
                : (await catalog.FindAsync(row.Existing.ApplicationId))!;
            if (row.Existing is null) { catalog.Add(app); created++; }
            if (row.Item.Code == "GATEWAY") app.IsPublic = true;
            if (row.Item.Code == "PAYSLIP" && !app.AccessGrants.Any(g => g.UserId == principals.User.UserId))
                app.AccessGrants.Add(new() { UserId = principals.User.UserId, CreatedAt = DateTime.UtcNow });
            if (row.Item.Code == "ATTENDANCE" && !app.AccessGrants.Any(g => g.RoleId == principals.Role.RoleId))
                app.AccessGrants.Add(new() { RoleId = principals.Role.RoleId, CreatedAt = DateTime.UtcNow });
        }
        await catalog.SaveAsync(); return new(created, plan.Count - created);
    }
}
