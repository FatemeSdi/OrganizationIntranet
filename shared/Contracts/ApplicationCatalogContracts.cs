namespace OrganizationIntranet.Contracts;

public static class ApplicationCatalogOptions
{
    public static readonly string[] Icons = ["apps", "document", "clock", "mail", "building", "users", "folder", "tasks", "message", "search", "upload", "archive", "chart"];
    public const string NotificationsService = "NOTIFICATIONS";
}

public record ApplicationServiceRequest(string ServiceCode, string DisplayName, string? EndpointUrl, bool IsActive = true);
public record ApplicationConfigurationRequest(ApplicationRequest Application, List<long> UserIds, List<int> RoleIds, List<ApplicationServiceRequest> Services);
public record ApplicationConfigurationDto(ApplicationDto Application, List<long> UserIds, List<int> RoleIds, List<ApplicationServiceRequest> Services, List<ApplicationServiceSyncDto>? SyncStatuses = null);
public record ApplicationServiceSyncDto(string ServiceCode, DateTime? LastSyncedAt, string? LastSyncError);
public record PortalApplicationDto(int ApplicationId, string Name, string? Description, string? Url, string Icon,
    bool RequiresLogin, bool IsInternetAccessible, bool IsPublic, int UnreadCount, List<string> Services);
public record PortalNotificationDto(long Id, string System, string Title, DateTime CreatedAt, bool Unread, string? TargetUrl = null);
public record NotificationDestinationDto(string Url);
