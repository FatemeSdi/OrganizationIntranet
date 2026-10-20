namespace OrganizationIntranet.Domain.Entities;

public class ApplicationAccessRequest
{
    public long ApplicationAccessRequestId { get; set; }
    public int ApplicationId { get; set; }
    public long UserId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public long? ReviewedBy { get; set; }
    public string? ReviewNote { get; set; }
    public Guid Revision { get; set; } = Guid.NewGuid();
    public Application Application { get; set; } = null!;
    public User User { get; set; } = null!;
    public User? Reviewer { get; set; }
}
