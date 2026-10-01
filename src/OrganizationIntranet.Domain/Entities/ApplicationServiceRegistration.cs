namespace OrganizationIntranet.Domain.Entities;

public class ApplicationServiceRegistration
{
    public int ApplicationServiceRegistrationId { get; set; }
    public int ApplicationId { get; set; }
    public string ServiceCode { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? EndpointUrl { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public string? SyncCursor { get; set; }
    public DateTime? LastSyncedAt { get; set; }
    public string? LastSyncError { get; set; }
    public Application Application { get; set; } = null!;
}
