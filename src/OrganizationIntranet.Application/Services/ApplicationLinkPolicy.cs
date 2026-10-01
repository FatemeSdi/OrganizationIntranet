namespace OrganizationIntranet.Application.Services;

public static class ApplicationLinkPolicy
{
    public static string? Destination(string? applicationUrl, string? targetUrl)
    {
        if (!Uri.TryCreate(applicationUrl, UriKind.Absolute, out var origin) || origin.Scheme is not ("https" or "http") || origin.UserInfo.Length > 0) return null;
        if (!Uri.TryCreate(origin, string.IsNullOrWhiteSpace(targetUrl) ? applicationUrl : targetUrl, out var target)
            || target.Scheme != origin.Scheme || !string.Equals(target.IdnHost, origin.IdnHost, StringComparison.OrdinalIgnoreCase)
            || target.Port != origin.Port || target.UserInfo.Length > 0 || target.AbsoluteUri.Length > 500) return null;
        return target.AbsoluteUri;
    }
}
