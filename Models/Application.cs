namespace OrganizationIntranet.Models;

public class Application
{
    public int ApplicationId { get; set; }
    public string ApplicationName { get; set; } = string.Empty;
    public string ApplicationCode { get; set; } = string.Empty;
    public string? BaseUrl { get; set; }
    public string? Description { get; set; }
    public string? Icon { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }

    public ICollection<Permission> Permissions { get; set; } = new List<Permission>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}
