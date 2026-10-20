namespace OrganizationIntranet.Contracts;

/// <summary>
/// کارت سامانه برای بازدیدکننده‌ی ناشناس. عمداً شمارنده‌ی اعلان، فهرست سرویس‌ها، کد و تنظیمات دسترسی را ندارد؛
/// فقط همان چیزی که برای نمایش کارت عمومی لازم است.
/// </summary>
public record PublicApplicationDto(int ApplicationId, string Name, string? Description, string? Url, string Icon, bool RequiresLogin, bool IsInternetAccessible);
