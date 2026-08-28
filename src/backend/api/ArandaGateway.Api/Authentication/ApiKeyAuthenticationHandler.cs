using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace ArandaGateway.Api.Authentication;

public sealed class ApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> schemeOptions,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IOptions<GatewayOptions> gatewayOptions)
    : AuthenticationHandler<AuthenticationSchemeOptions>(
        schemeOptions,
        logger,
        encoder)
{
    public const string SchemeName = "GatewayApiKey";
    public const string HeaderName = "X-Api-Key";

    private readonly byte[] expectedApiKey =
        Encoding.UTF8.GetBytes(gatewayOptions.Value.ApiKey);

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var values = Request.Headers[HeaderName];
        if (values.Count != 1 || string.IsNullOrWhiteSpace(values[0]))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var suppliedApiKey = Encoding.UTF8.GetBytes(values[0]!);
        if (suppliedApiKey.Length != expectedApiKey.Length ||
            !CryptographicOperations.FixedTimeEquals(
                suppliedApiKey,
                expectedApiKey))
        {
            return Task.FromResult(
                AuthenticateResult.Fail("Invalid gateway API key."));
        }

        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "apim")],
            SchemeName);
        var principal = new ClaimsPrincipal(identity);
        return Task.FromResult(
            AuthenticateResult.Success(
                new AuthenticationTicket(principal, SchemeName)));
    }
}
