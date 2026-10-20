namespace OrganizationIntranet.Application.Abstractions;

public interface IAccessRequestSmsSender
{
    Task SendAsync(string mobile, string message, string idempotencyKey, CancellationToken cancellationToken);
}
