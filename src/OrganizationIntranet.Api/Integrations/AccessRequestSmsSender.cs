using System.Net.Http.Headers;
using System.Text.Json;
using OrganizationIntranet.Application.Abstractions;
using OrganizationIntranet.Contracts;

namespace OrganizationIntranet.Api.Integrations;

public sealed class AccessRequestSmsSender(IAuthenticationRepository settingsRepository, IAuthenticationProviders protection, IHttpClientFactory clients) : IAccessRequestSmsSender
{
    public async Task SendAsync(string mobile, string message, string idempotencyKey, CancellationToken cancellationToken)
    {
        var row = await settingsRepository.SettingsAsync();
        var settings = JsonSerializer.Deserialize<AuthenticationSettingsDto>(row.Json) ?? new();
        if (string.IsNullOrWhiteSpace(row.ProtectedSmsApiKey) || !Uri.TryCreate(settings.SmsEndpoint, UriKind.Absolute, out var endpoint)
            || endpoint.Scheme != "https" || endpoint.UserInfo.Length > 0)
            throw new InvalidOperationException("SMS transport is not configured.");
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", protection.Unprotect(row.ProtectedSmsApiKey));
        request.Headers.Add("Idempotency-Key", idempotencyKey);
        request.Content = JsonContent.Create(new { to = mobile, sender = settings.SmsSender, message });
        using var response = await clients.CreateClient("access-request-sms").SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
