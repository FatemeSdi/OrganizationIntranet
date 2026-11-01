using OrganizationIntranet.Domain.Entities;
using AppEntity = OrganizationIntranet.Domain.Entities.Application;

namespace OrganizationIntranet.Application.Abstractions;

/// <summary>دسترسی به داده‌ی درخواست‌های دسترسی؛ پیاده‌سازی در لایه‌ی API (EF Core).</summary>
public interface IAccessRequestRepository
{
    /// <summary>سامانه‌های قابل درخواست برای کاربر.</summary>
    Task<List<AppEntity>> RequestableAsync(long userId);
    /// <summary>صفحه‌ای از درخواست‌ها؛ userId خالی = همه‌ی کاربران.</summary>
    Task<(List<ApplicationAccessRequest> Items, int Total)> ListAsync(long? userId, string? status, int page, int pageSize);
    /// <summary>یک درخواست؛ اگر userId داده شود فقط وقتی مال همان کاربر باشد.</summary>
    Task<ApplicationAccessRequest?> FindAsync(long id, long? userId = null);
    /// <summary>آیا کاربر می‌تواند برای این سامانه درخواست بدهد (فعال، بدون دسترسی و بدون درخواست باز).</summary>
    Task<bool> CanRequestAsync(long userId, int applicationId);
    /// <summary>آیا کاربر همین الان به سامانه دسترسی مستقیم یا از راه نقش دارد.</summary>
    Task<bool> HasAccessAsync(long userId, int applicationId);
    /// <summary>درخواست تازه را به واحد کار اضافه می‌کند (تا SaveAsync ذخیره نمی‌شود).</summary>
    void Add(ApplicationAccessRequest request);
    /// <summary>دسترسی مستقیم کاربر به سامانه‌ی درخواست را می‌سازد؛ همراه تصمیم مدیر در یک تراکنش ذخیره می‌شود.</summary>
    void Grant(ApplicationAccessRequest request);
    /// <summary>همه‌ی تغییرات را در یک تراکنش ذخیره می‌کند.</summary>
    Task SaveAsync();
}
