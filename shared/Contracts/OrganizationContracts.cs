namespace OrganizationIntranet.Contracts;

/// <summary>یک واحد چارت سازمانی برای فهرست درختی پنل. فهرست به ترتیب پیمایش درخت (پدر پیش از فرزندان) می‌آید و Depth از صفر برای ریشه است.</summary>
public record OrgUnitDto(int UnitId, int? ParentId, string Title, string FullPath, int Depth, int DisplayOrder, bool IsActive, int ChildCount, int UserCount);
/// <summary>ساخت یا ویرایش واحد؛ ParentId خالی یعنی ریشه. جابه‌جایی واحد همین ویرایش با ParentId تازه است.</summary>
public record OrgUnitRequest(string? Title, int? ParentId, int DisplayOrder, bool IsActive = true);
