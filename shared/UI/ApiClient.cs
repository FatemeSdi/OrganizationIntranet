using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using OrganizationIntranet.Contracts;

namespace OrganizationIntranet.UI;

public sealed class ApiException(HttpStatusCode status, string message) : Exception(message)
{
    public HttpStatusCode Status { get; } = status;
}

public sealed class ApiClient(HttpClient client, IHttpContextAccessor accessor)
{
    public async Task<T> GetAsync<T>(string path, string? token = null) => await SendAsync<T>(HttpMethod.Get, path, null, token);
    public async Task<T> PostAsync<T>(string path, object? value = null) => await SendAsync<T>(HttpMethod.Post, path, value);
    private async Task<T> SendAsync<T>(HttpMethod method, string path, object? value, string? token = null)
    {
        using var request = new HttpRequestMessage(method, path);
        token ??= accessor.HttpContext?.User.FindFirst("api_token")?.Value;
        if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (value is not null) request.Content = JsonContent.Create(value);
        try
        {
            using var response = await client.SendAsync(request, accessor.HttpContext?.RequestAborted ?? default);
            if (!response.IsSuccessStatusCode)
            {
                var message = response.StatusCode == HttpStatusCode.TooManyRequests ? "تعداد درخواست‌ها بیش از حد مجاز است؛ کمی بعد تلاش کنید" : "درخواست ناموفق بود؛ دوباره تلاش کنید";
                try { message = (await response.Content.ReadFromJsonAsync<OperationResult>())?.Message ?? message; } catch (JsonException) { }
                throw new ApiException(response.StatusCode, message);
            }
            return (await response.Content.ReadFromJsonAsync<T>()) ?? throw new ApiException(HttpStatusCode.BadGateway, "پاسخ سرویس معتبر نیست");
        }
        catch (HttpRequestException) { throw new ApiException(HttpStatusCode.ServiceUnavailable, "ارتباط با سرویس برقرار نشد"); }
        catch (TaskCanceledException) when (!(accessor.HttpContext?.RequestAborted.IsCancellationRequested ?? false)) { throw new ApiException(HttpStatusCode.GatewayTimeout, "زمان پاسخ سرویس به پایان رسید"); }
    }
}

public record TokenResponse(string AccessToken, int ExpiresIn, string RefreshToken);
