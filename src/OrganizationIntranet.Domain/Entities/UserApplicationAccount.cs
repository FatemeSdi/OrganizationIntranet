namespace OrganizationIntranet.Domain.Entities;

/// <summary>
/// نام کاربری حساب یک کاربر اینترانت در یک سامانه‌ی بیرونی، وقتی با نام کاربری اینترانت فرق دارد (docs/sso.md).
/// ورود یکپارچه همین نام را به سامانه می‌دهد، پس سامانه نه تغییری می‌کند و نه جدولی لازم دارد. یک حساب سامانه فقط به یک کاربر اینترانت تعلق دارد.
/// </summary>
public class UserApplicationAccount
{
    public long UserApplicationAccountId { get; set; }
    public long UserId { get; set; }
    public int ApplicationId { get; set; }
    public string ExternalUsername { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public long UpdatedBy { get; set; }
    public User User { get; set; } = null!;
    public Application Application { get; set; } = null!;
}
