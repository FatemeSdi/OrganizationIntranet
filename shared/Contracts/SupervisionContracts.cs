namespace OrganizationIntranet.Contracts;

public static class AccessRequestChannels
{
    public static bool Valid(string value) => value is "InApp" or "Sms" or "Both";
    public static string Label(string value) => value switch { "InApp" => "درون‌برنامه‌ای", "Sms" => "پیامک موبایل", "Both" => "درون‌برنامه‌ای و پیامک", _ => value };
}
public record SupervisionGroupDto(int Id, int ApplicationId, string Name, bool IsActive, List<long> UserIds, List<int> RoleIds, Guid Revision);
public record SupervisionGroupRequest(int Id, string Name, bool IsActive, List<long> UserIds, List<int> RoleIds, Guid Revision);
public record AccessRequestAlertDto(long Id, long RequestId, string ApplicationName, string Requester, DateTime CreatedAt, bool IsRead);
public record AccessRequestAlertsDto(List<AccessRequestAlertDto> Items, int UnreadCount);
public record AccessRequestSmsDto(long Id, long RequestId, string ApplicationName, string Recipient, string Status, int Attempts, string? Error, DateTime? SentAt);
public record AccessRequestSmsListDto(List<AccessRequestSmsDto> Items, int Total, int Page, int PageSize);
