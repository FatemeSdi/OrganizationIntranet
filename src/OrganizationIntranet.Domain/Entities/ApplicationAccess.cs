namespace OrganizationIntranet.Domain.Entities;

public class ApplicationAccess
{
    public long ApplicationAccessId { get; set; }
    public int ApplicationId { get; set; }
    public long? UserId { get; set; }
    public int? RoleId { get; set; }
    public DateTime CreatedAt { get; set; }
    public Application Application { get; set; } = null!;
    public User? User { get; set; }
    public Role? Role { get; set; }
}
