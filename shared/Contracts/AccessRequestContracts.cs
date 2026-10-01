namespace OrganizationIntranet.Contracts;

public static class AccessRequestStatuses
{
    public const string Pending = "Pending", Approved = "Approved", Rejected = "Rejected", Cancelled = "Cancelled";
    public static string Label(string status) => status switch
    { Pending => "در انتظار بررسی", Approved => "تأییدشده", Rejected => "ردشده", Cancelled => "لغوشده", _ => status };
    public static bool IsValid(string status) => status is Pending or Approved or Rejected or Cancelled;
}
public record RequestableApplicationDto(int ApplicationId, string Name, string? Description);
public record CreateAccessRequest(int ApplicationId, string Reason);
public record ReviewAccessRequest(bool Approve, string? Note, Guid Revision);
public record CancelAccessRequest(Guid Revision);
public record AccessRequestDto(long Id, int ApplicationId, string ApplicationName, long UserId, string Username,
    string DisplayName, string Reason, string Status, DateTime CreatedAt, DateTime? ResolvedAt, string? Reviewer,
    string? ReviewNote, Guid Revision);
public record AccessRequestListDto(List<AccessRequestDto> Items, int Total, int Page, int PageSize);
