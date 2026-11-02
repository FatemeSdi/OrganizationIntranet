namespace OrganizationIntranet.Contracts;

/// <summary>یک سامانه‌ی فهرست مرجع با تخصیص اولیه‌اش و اینکه از قبل در کاتالوگ هست.</summary>
public record CatalogImportItemDto(string Code, string Name, string Description, string Access, bool Exists);
/// <summary>پیش‌نمایش ورود فهرست مرجع، با کاربر و نقشی که تخصیص اولیه به آن‌ها داده می‌شود.</summary>
public record CatalogImportPreviewDto(List<CatalogImportItemDto> Items, string Username, string RoleName);
/// <summary>نتیجه‌ی ورود فهرست مرجع: چند سامانه ساخته شد و چند مورد از قبل بود.</summary>
public record CatalogImportResultDto(int Created, int Existing);
