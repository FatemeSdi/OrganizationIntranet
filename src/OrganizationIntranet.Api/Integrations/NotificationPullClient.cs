using System.Text.Json;
using OrganizationIntranet.Contracts;

namespace OrganizationIntranet.Api.Integrations;

// The exact endpoint and its credential are pinned in server configuration, separate from UI client keys.
public sealed class NotificationPullClient(HttpClient client, IConfiguration configuration)
{
    public async Task<ApplicationNotificationBatch> FetchAsync(string applicationCode, string endpoint, string? cursor, CancellationToken cancellationToken)
    {
        var settings = configuration.GetSection($"ApplicationIntegrations:{applicationCode}");
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var url) || url.Scheme != "https" || url.UserInfo.Length > 0 || url.Fragment.Length > 0
            || !Uri.TryCreate(settings["PullUrl"], UriKind.Absolute, out var pinned) || !string.Equals(url.AbsoluteUri, pinned.AbsoluteUri, StringComparison.Ordinal))
            throw new InvalidOperationException("Notification endpoint is not approved in server configuration.");
        var key = settings["PullKey"];
        if (string.IsNullOrWhiteSpace(key) || key.Length < 32 || key.Contains('\r') || key.Contains('\n')
            || key == configuration["Api:ClientKey"] || key == configuration["Api:AdminClientKey"])
            throw new InvalidOperationException("Configure a dedicated source credential.");
        var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(url.Query);
        if (query.ContainsKey("cursor")) throw new InvalidOperationException("The registered endpoint must not contain a cursor.");
        var destination = string.IsNullOrEmpty(cursor) ? url.AbsoluteUri : Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString(url.AbsoluteUri, "cursor", cursor);
        using var request = new HttpRequestMessage(HttpMethod.Get, destination);
        request.Headers.Add("X-Intranet-Integration-Key", key);
        request.Headers.Accept.ParseAdd("application/json");
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode) throw new HttpRequestException("Source notification service returned an error.");
        if (response.Content.Headers.ContentLength > 262144) throw new InvalidOperationException("Source response is too large.");
        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        var chunk = new byte[8192]; int count;
        while ((count = await input.ReadAsync(chunk, cancellationToken)) != 0)
        {
            if (buffer.Length + count > 262144) throw new InvalidOperationException("Source response is too large.");
            await buffer.WriteAsync(chunk.AsMemory(0, count), cancellationToken);
        }
        return JsonSerializer.Deserialize<ApplicationNotificationBatch>(buffer.ToArray(), new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException("Invalid source response.");
    }
}
