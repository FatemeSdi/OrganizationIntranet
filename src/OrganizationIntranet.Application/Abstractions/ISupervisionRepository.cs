using OrganizationIntranet.Domain.Entities;

namespace OrganizationIntranet.Application.Abstractions;

public interface ISupervisionRepository
{
    Task<List<ApplicationSupervisionGroup>> GroupsAsync(int applicationId);
    Task<ApplicationSupervisionGroup?> GroupAsync(int applicationId, int id);
    Task<bool> ValidPrincipalsAsync(IReadOnlyCollection<long> users, IReadOnlyCollection<int> roles);
    void AddGroup(ApplicationSupervisionGroup group);
    void SetMembers(ApplicationSupervisionGroup group, IReadOnlyCollection<long> users, IReadOnlyCollection<int> roles);
    Task EnqueueAsync(ApplicationAccessRequest request);
    Task<(List<AccessRequestAlert> Items, int UnreadCount)> AlertsAsync(long userId);
    Task MarkReadAsync(long userId, long? alertId);
    Task<(List<ApplicationAccessRequest> Items, int Total)> InboxAsync(long userId, int page, int pageSize);
    Task<(List<AccessRequestAlert> Items, int Total)> SmsAsync(int page, int pageSize);
    Task<AccessRequestAlert?> FindSmsAsync(long id);
    Task SaveAsync();
}
