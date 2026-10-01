namespace OrganizationIntranet.Domain.Entities;

public class ApplicationSupervisionGroup
{
    public int ApplicationSupervisionGroupId { get; set; }
    public int ApplicationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public Guid Revision { get; set; } = Guid.NewGuid();
    public Application Application { get; set; } = null!;
    public ICollection<ApplicationSupervisor> Members { get; set; } = new List<ApplicationSupervisor>();
}
public class ApplicationSupervisor
{
    public long ApplicationSupervisorId { get; set; }
    public int ApplicationSupervisionGroupId { get; set; }
    public long? UserId { get; set; }
    public int? RoleId { get; set; }
    public ApplicationSupervisionGroup Group { get; set; } = null!;
    public User? User { get; set; }
    public Role? Role { get; set; }
}
