namespace OrganizationIntranet.Domain.Entities;

/// <summary>سرویسی که یک سامانه ثبت کرده است. سرویس NOTIFICATIONS یعنی سامانه اعلان می‌فرستد (Push) یا اینترانت از نشانی آن اعلان می‌گیرد (Pull).</summary>
public class ApplicationServiceRegistration
{
    public int ApplicationServiceRegistrationId { get; set; }
    public int ApplicationId { get; set; }
    public string ServiceCode { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? EndpointUrl { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    /// <summary>نشانگر همگام‌سازی Pull: دور بعد از همین‌جا ادامه می‌یابد. با تغییر نشانی سرویس پاک می‌شود.</summary>
    public string? SyncCursor { get; set; }
    public DateTime? LastSyncedAt { get; set; }
    public string? LastSyncError { get; set; }
    public Application Application { get; set; } = null!;
}
