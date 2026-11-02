using Microsoft.EntityFrameworkCore;
using OrganizationIntranet.Application.Abstractions;
using OrganizationIntranet.Domain.Entities;

namespace OrganizationIntranet.Api.Data;

/// <summary>پیاده‌سازی EF Core داده‌ی چارت سازمانی (Org.Unit). واحدها ردیابی می‌شوند تا تغییر مسیر زیرمجموعه‌ها در یک SaveChanges ذخیره شود.</summary>
public sealed class OrganizationRepository(AppDbContext db) : IOrganizationRepository
{
    public Task<List<OrgUnit>> GetUnitsAsync() => db.OrgUnits.ToListAsync();
    public Task<Dictionary<int, int>> GetUserCountsAsync() =>
        db.Users.Where(u => u.UnitId != null).GroupBy(u => u.UnitId!.Value).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(g => g.Key, g => g.Count);
    public Task<OrgUnit?> FindUnitAsync(int id) => db.OrgUnits.FirstOrDefaultAsync(u => u.UnitId == id);
    public void AddUnit(OrgUnit unit) => db.OrgUnits.Add(unit);
    public void RemoveUnit(OrgUnit unit) => db.OrgUnits.Remove(unit);
    public Task<bool> HasUsersAsync(int unitId) => db.Users.AnyAsync(u => u.UnitId == unitId);
    public Task SaveAsync() => db.SaveChangesAsync();
}
