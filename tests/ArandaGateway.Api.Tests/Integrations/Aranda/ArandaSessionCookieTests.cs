using System.Net;
using ArandaGateway.Api.Integrations.Aranda;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ArandaGateway.Api.Tests.Integrations.Aranda;

public sealed class ArandaSessionCookieTests
{
    [Fact]
    public void Value_StartsFromConfiguredSeed()
    {
        var cookie = CreateCookie("AuthCookieASMS=SEMILLA");

        Assert.Equal("AuthCookieASMS=SEMILLA", cookie.Value);
        Assert.Null(cookie.RenewedAt);
    }

    [Fact]
    public void Value_IsNullWhenNoCookieConfigured()
    {
        Assert.Null(CreateCookie(null).Value);
    }

    [Fact]
    public void Renew_DropsAttributesThatDoNotTravelInCookieHeader()
    {
        var cookie = CreateCookie("AuthCookieASMS=SEMILLA");

        cookie.Renew("AuthCookieASMS=NUEVA; path=/; HttpOnly; SameSite=Lax");

        Assert.Equal("AuthCookieASMS=NUEVA", cookie.Value);
        Assert.NotNull(cookie.RenewedAt);
    }

    [Fact]
    public void Renew_IgnoresBlankValues()
    {
        var cookie = CreateCookie("AuthCookieASMS=SEMILLA");

        cookie.Renew("   ");

        Assert.Equal("AuthCookieASMS=SEMILLA", cookie.Value);
    }

    [Fact]
    public async Task Handler_SendsCurrentCookieAndStoresTheRenewedOne()
    {
        var cookie = CreateCookie("AuthCookieASMS=SEMILLA");
        var transport = new StubTransport(
            "AuthCookieASMS=RENOVADA; path=/; HttpOnly");
        using var client = CreateClient(cookie, transport);

        await client.GetAsync("https://aranda.example/api/v9/item/1");

        Assert.Equal("AuthCookieASMS=SEMILLA", transport.LastCookieSent);
        Assert.Equal("AuthCookieASMS=RENOVADA", cookie.Value);
    }

    [Fact]
    public async Task Handler_UsesRenewedCookieOnTheNextRequest()
    {
        var cookie = CreateCookie("AuthCookieASMS=SEMILLA");
        var transport = new StubTransport(
            "AuthCookieASMS=RENOVADA; path=/; HttpOnly");
        using var client = CreateClient(cookie, transport);

        await client.GetAsync("https://aranda.example/api/v9/item/1");
        await client.GetAsync("https://aranda.example/api/v9/item/2");

        Assert.Equal("AuthCookieASMS=RENOVADA", transport.LastCookieSent);
    }

    [Fact]
    public async Task Handler_KeepsCurrentCookieWhenResponseDoesNotRenewIt()
    {
        var cookie = CreateCookie("AuthCookieASMS=SEMILLA");
        var transport = new StubTransport(setCookie: null);
        using var client = CreateClient(cookie, transport);

        await client.GetAsync("https://aranda.example/api/v9/item/1");

        Assert.Equal("AuthCookieASMS=SEMILLA", cookie.Value);
    }

    [Fact]
    public async Task Handler_SendsNoCookieHeaderWhenNoneIsKnown()
    {
        var cookie = CreateCookie(null);
        var transport = new StubTransport(setCookie: null);
        using var client = CreateClient(cookie, transport);

        await client.GetAsync("https://aranda.example/api/v9/item/1");

        Assert.Null(transport.LastCookieSent);
    }

    [Fact]
    public void Value_IgnoresSeedWhenSessionCookieIsDisabled()
    {
        var cookie = CreateCookie("AuthCookieASMS=SEMILLA", enabled: false);

        Assert.Null(cookie.Value);
    }

    [Fact]
    public async Task Handler_DoesNotAdoptRenewedCookieWhenDisabled()
    {
        // Sin esto el interruptor se encendería solo: basta un Set-Cookie de
        // Aranda para que el gateway volviera a mandar cookie en la siguiente
        // petición, y con ella el 401 de la cookie caducada.
        var cookie = CreateCookie("AuthCookieASMS=SEMILLA", enabled: false);
        var transport = new StubTransport("AuthCookieASMS=RENOVADA; path=/");
        using var client = CreateClient(cookie, transport);

        await client.GetAsync("https://aranda.example/api/v9/item/1");
        await client.GetAsync("https://aranda.example/api/v9/item/2");

        Assert.Null(transport.LastCookieSent);
        Assert.Null(cookie.Value);
    }

    [Fact]
    public void Install_TurnsTheCookieBackOnWhileDisabled()
    {
        // La vía de soporte: si Aranda vuelve a exigir sesión, se instala una
        // cookie por PUT /admin/aranda-session y el envío se reactiva en
        // caliente, sin redesplegar ni tocar configuración.
        var cookie = CreateCookie("AuthCookieASMS=SEMILLA", enabled: false);

        cookie.Install("AuthCookieASMS=MANUAL; path=/");

        Assert.Equal("AuthCookieASMS=MANUAL", cookie.Value);
        Assert.NotNull(cookie.RenewedAt);
    }

    [Fact]
    public async Task Handler_ResumesAdoptingCookiesAfterInstall()
    {
        var cookie = CreateCookie("AuthCookieASMS=SEMILLA", enabled: false);
        var transport = new StubTransport("AuthCookieASMS=RENOVADA; path=/");
        using var client = CreateClient(cookie, transport);

        cookie.Install("AuthCookieASMS=MANUAL");
        await client.GetAsync("https://aranda.example/api/v9/item/1");

        Assert.Equal("AuthCookieASMS=MANUAL", transport.LastCookieSent);
        Assert.Equal("AuthCookieASMS=RENOVADA", cookie.Value);
    }

    private static ArandaSessionCookie CreateCookie(
        string? seed,
        bool enabled = true) =>
        new(Options.Create(new ArandaOptions
        {
            BaseUrl = new("https://aranda.example/"),
            ApiKey = "Bearer test",
            ProjectId = 1,
            AuthorId = 2,
            AuthCookie = seed,
            SessionCookieEnabled = enabled
        }));

    private static HttpClient CreateClient(
        ArandaSessionCookie cookie,
        StubTransport transport)
    {
        var handler = new ArandaSessionCookieHandler(
            cookie,
            NullLogger<ArandaSessionCookieHandler>.Instance)
        {
            InnerHandler = transport
        };

        return new(handler);
    }

    private sealed class StubTransport(string? setCookie) : HttpMessageHandler
    {
        public string? LastCookieSent { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastCookieSent = request.Headers.TryGetValues(
                "Cookie",
                out var values)
                ? string.Join("; ", values)
                : null;

            var response = new HttpResponseMessage(HttpStatusCode.OK);
            if (setCookie is not null)
            {
                response.Headers.TryAddWithoutValidation(
                    "Set-Cookie",
                    setCookie);
            }

            return Task.FromResult(response);
        }
    }
}
