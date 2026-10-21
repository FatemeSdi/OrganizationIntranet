namespace OrganizationIntranet.Pages.Shared;

/// <summary>مدل partial «_EmptyState»: حالت خالی فهرست‌ها با آیکن، توضیح و اقدام پیشنهادی.</summary>
public sealed record EmptyState(string Icon, string Title, string? Text = null, string? ActionText = null, string? ActionUrl = null);
