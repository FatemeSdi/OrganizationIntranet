namespace OrganizationIntranet.Domain.Entities;

/// <summary>هشدارِ یک درخواست دسترسی برای یک ناظر یا بررسی‌کننده: وضعیت درون‌برنامه‌ای (InApp، IsRead) و وضعیت پیامک (SmsStatus: NotRequested، Pending، Processing، Sent، Failed، Cancelled).</summary>
public class AccessRequestAlert
{
    public long AccessRequestAlertId { get; set; }
    public long ApplicationAccessRequestId { get; set; }
    public long UserId { get; set; }
    public bool InApp { get; set; }
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public string SmsStatus { get; set; } = "NotRequested";
    public int SmsAttempts { get; set; }
    public DateTime? NextAttemptAt { get; set; }
    public DateTime? SmsSentAt { get; set; }
    public string? SmsError { get; set; }
    public Guid Revision { get; set; } = Guid.NewGuid();
    public ApplicationAccessRequest Request { get; set; } = null!;
    public User User { get; set; } = null!;
}
