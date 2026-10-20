namespace OrganizationIntranet.Contracts;

public record CatalogImportItemDto(string Code, string Name, string Description, string Access, bool Exists);
public record CatalogImportPreviewDto(List<CatalogImportItemDto> Items, string Username, string RoleName);
public record CatalogImportResultDto(int Created, int Existing);
