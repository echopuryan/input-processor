using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;

namespace API.Authentication;

/// <summary>
/// Basic authentication handler for the Nginx auth
/// </summary>
public class BasicAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "NginxBasic";

    public BasicAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : base(options, logger, encoder) { }


    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var username = Request.Headers["X-Auth-User"].FirstOrDefault();

        if (string.IsNullOrEmpty(username))
        {
            if (!AuthenticationHeaderValue.TryParse(
                    Request.Headers.Authorization, out var authHeader) ||
                !string.Equals(authHeader.Scheme, "Basic", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var credentialBytes = Convert.FromBase64String(authHeader.Parameter!);
            var credentials = Encoding.UTF8.GetString(credentialBytes).Split(':', 2);
            username = credentials[0];
        }

        if (string.IsNullOrEmpty(username))
            return Task.FromResult(AuthenticateResult.Fail("No user identified."));

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, username),
            new Claim(ClaimTypes.NameIdentifier, username),
        };

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
        throw new NotImplementedException();
    }
}
