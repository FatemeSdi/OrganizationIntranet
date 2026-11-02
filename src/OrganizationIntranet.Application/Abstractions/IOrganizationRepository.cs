using OrganizationIntranet.Domain.Entities;

namespace OrganizationIntranet.Application.Abstractions;

/// <summary>دسترسی به داده‌ی چارت سازمانی؛ پیاده‌سازی در لایه‌ی API. موجودیت‌های برگشتی تا پایان همان درخواست ردیابی می‌شوند.</summary>
public interface IOrganizationRepository
{
    /// <summary>همه‌ی واحدها (ردیابی‌شده) برای ساخت درخت و بازسازی مسیر زیرمجموعه.</summary>
    Task<List<OrgUnit>> GetUnitsAsync();
    /// <summary>شمار کاربران مستقیم هر واحد.</summary>
    Task<Dictionary<int, int>> GetUserCountsAsync();
    Task<OrgUnit?> FindUnitAsync(int id);
    void AddUnit(OrgUnit unit);
    void RemoveUnit(OrgUnit unit);
    /// <summary>آیا کاربری به این واحد وصل است؟</summary>
    Task<bool> HasUsersAsync(int unitId);
    /// <summary>تغییرات را در یک تراکنش ذخیره می‌کند.</summary>
    Task SaveAsync();
}
