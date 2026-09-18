using System.ComponentModel.DataAnnotations;

namespace OrganizationIntranet.Contracts;

public static class PublicationKinds
{
    public static bool IsValid(string? kind) => kind is "News" or "Announcement" or "Circular";
    public static string Label(string kind) => kind switch { "News" => "خبر", "Announcement" => "اطلاعیه", "Circular" => "بخشنامه", _ => "نامعتبر" };
}
public sealed class PublicationEditRequest
{
    [Required, StringLength(200)] public string Title { get; set; } = "";
    [Required, StringLength(1000)] public string Summary { get; set; } = "";
    [Required, StringLength(50000)] public string Body { get; set; } = "";
    [Required] public string Kind { get; set; } = "News";
    public bool IsPublished { get; set; }
    [Range(0, long.MaxValue)] public long? Revision { get; set; }
}
public record PublicationDto(long PublicationId, string Title, string Summary, string Body, string Kind, bool IsPublished, DateTime CreatedAt, DateTime UpdatedAt, DateTime? PublishedAt, long Revision = 0);
public record PublicationListDto(List<PublicationDto> Items, int Total, int Page, int PageSize);
