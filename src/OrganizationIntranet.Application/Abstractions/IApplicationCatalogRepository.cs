using OrganizationIntranet.Domain.Entities;
using AppEntity = OrganizationIntranet.Domain.Entities.Application;

namespace OrganizationIntranet.Application.Abstractions;

public interface IApplicationCatalogRepository
{
    Task<List<AppEntity>> ListAsync();
    Task<AppEntity?> FindAsync(int id);
    Task<List<AppEntity>> AvailableAsync(long userId);
    Task<List<UserNotification>> NotificationsAsync(long userId, bool unreadOnly, int? limit = null);
    Task<Dictionary<int, int>> UnreadCountsAsync(long userId);
    Task<UserNotification?> FindNotificationAsync(long userId, long id);
    Task<bool> PrincipalsExistAsync(IReadOnlyCollection<long> userIds, IReadOnlyCollection<int> roleIds);
    void Add(AppEntity application);
    void SetAccess(AppEntity application, IReadOnlyCollection<long> userIds, IReadOnlyCollection<int> roleIds);
    void SetServices(AppEntity application, IReadOnlyCollection<ApplicationServiceRegistration> services);
    Task SaveAsync();
}
