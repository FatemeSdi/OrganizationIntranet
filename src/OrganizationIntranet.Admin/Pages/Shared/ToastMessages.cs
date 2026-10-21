namespace OrganizationIntranet.Pages.Shared;

/// <summary>نتیجه‌ی یک عملیات که به‌صورت toast تمپلیت yzen نمایش داده می‌شود (partial: _ToastMessages).</summary>
public sealed record ToastMessages(string? Success, string? Error = null);
