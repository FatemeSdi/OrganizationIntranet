using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace OrganizationIntranet.Api.Security;

public sealed class ApplicationIntegrationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger,
    UrlEncoder encoder, IConfiguration configuration) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "ApplicationIntegration";
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var code = Context.Request.RouteValues["applicationCode"]?.ToString()?.ToUpperInvariant();
        if (code is null || code.Length > 50 || !Regex.IsMatch(code, "^[A-Z0-9_-]+$")) return Task.FromResult(AuthenticateResult.Fail("Invalid application"));
        var expected = configuration[$"ApplicationIntegrations:{code}:PushKey"];
        var supplied = Request.Headers["X-Application-Key"].ToString();
        if (string.IsNullOrWhiteSpace(expected) || expected.Length < 32 || supplied.Length > 500
            || expected == configuration["Api:ClientKey"] || expected == configuration["Api:AdminClientKey"]
            || !CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(expected)), SHA256.HashData(Encoding.UTF8.GetBytes(supplied))))
            return Task.FromResult(AuthenticateResult.Fail("Invalid application credential"));
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("application_code", code)], SchemeName));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
    }
}
