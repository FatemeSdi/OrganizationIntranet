namespace OrganizationIntranet.Contracts;

// ExternalId must remain stable for this event and recipient in the source application.
public record IncomingApplicationNotification(string ExternalId, string Username, string Title, string Message,
    string TargetUrl, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, bool IsRead = false);
public record ApplicationNotificationBatch(List<IncomingApplicationNotification> Items, string? NextCursor = null);
public record NotificationImportResult(int Imported, int Ignored);
