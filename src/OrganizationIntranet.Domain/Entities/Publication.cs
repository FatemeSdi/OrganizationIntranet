namespace OrganizationIntranet.Domain.Entities;

public sealed class Publication
{
    public long PublicationId { get; set; }
    public string Title { get; set; } = "";
    public string Summary { get; set; } = "";
    public string Body { get; set; } = "";
    public string Kind { get; set; } = "News";
    public bool IsPublished { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? PublishedAt { get; set; }
    public long CreatedBy { get; set; }
    public long UpdatedBy { get; set; }
}
