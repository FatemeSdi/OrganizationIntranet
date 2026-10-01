using OrganizationIntranet.Domain.Entities;

namespace OrganizationIntranet.Application.Abstractions;

public interface INotificationIntegrationRepository
{
    Task<ApplicationServiceRegistration?> RegistrationAsync(string applicationCode);
    Task<User?> RecipientAsync(string username, int applicationId);
    Task<Notification?> ExistingAsync(int applicationId, long userId, string externalId);
    void Add(Notification notification);
    Task SaveAsync();
}
