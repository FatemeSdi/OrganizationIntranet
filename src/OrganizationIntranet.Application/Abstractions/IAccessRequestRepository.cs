using OrganizationIntranet.Domain.Entities;
using AppEntity = OrganizationIntranet.Domain.Entities.Application;

namespace OrganizationIntranet.Application.Abstractions;

public interface IAccessRequestRepository
{
    Task<List<AppEntity>> RequestableAsync(long userId);
    Task<(List<ApplicationAccessRequest> Items, int Total)> ListAsync(long? userId, string? status, int page, int pageSize);
    Task<ApplicationAccessRequest?> FindAsync(long id, long? userId = null);
    Task<bool> CanRequestAsync(long userId, int applicationId);
    Task<bool> HasAccessAsync(long userId, int applicationId);
    void Add(ApplicationAccessRequest request);
    void Grant(ApplicationAccessRequest request);
    Task SaveAsync();
}
