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
    public async Task Handler_InvalidatesRejectedCookieAndRetriesWithAuthorization()
    {
        var cookie = CreateCookie("AuthCookieASMS=VENCIDA");
        var transport = new StubTransport(
            setCookie: null,
            HttpStatusCode.Unauthorized,
            HttpStatusCode.OK);
        using var client = CreateClient(cookie, transport);

        using var response = await client.GetAsync(
            "https://aranda.example/api/v9/item/46479");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            ["AuthCookieASMS=VENCIDA", null],
            transport.CookiesSent);
        Assert.Equal(["Bearer test", "Bearer test"],
            transport.AuthorizationsSent);
        Assert.Equal(["apim-test", "apim-test"],
            transport.SubscriptionKeysSent);
        Assert.Null(cookie.Value);
    }

    [Fact]
    public async Task Handler_ReplaysPostBodyWithoutRejectedCookie()
    {
        const string body = "{\"subject\":\"test\"}";
        var cookie = CreateCookie("AuthCookieASMS=VENCIDA");
        var transport = new StubTransport(
            setCookie: null,
            HttpStatusCode.Unauthorized,
            HttpStatusCode.OK);
        using var client = CreateClient(cookie, transport);
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "https://aranda.example/api/v9/item/")
        {
            Content = new StringContent(body)
        };

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal([body, body], transport.BodiesSent);
        Assert.Equal(
            ["AuthCookieASMS=VENCIDA", null],
            transport.CookiesSent);
        Assert.Equal(["Bearer test", "Bearer test"],
            transport.AuthorizationsSent);
        Assert.Equal(["apim-test", "apim-test"],
            transport.SubscriptionKeysSent);
        Assert.Null(cookie.Value);
    }

    [Fact]
    public async Task Handler_DoesNotRetryUnauthorizedWhenNoCookieWasSent()
    {
        var cookie = CreateCookie(null);
        var transport = new StubTransport(
            setCookie: null,
            HttpStatusCode.Unauthorized);
        using var client = CreateClient(cookie, transport);

        using var response = await client.GetAsync(
            "https://aranda.example/api/v9/item/46479");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Single(transport.CookiesSent);
        Assert.Null(transport.CookiesSent[0]);
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

        var client = new HttpClient(handler);
        client.DefaultRequestHeaders.TryAddWithoutValidation(
            "X-Authorization",
            "Bearer test");
        client.DefaultRequestHeaders.TryAddWithoutValidation(
            "Ocp-Apim-Subscription-Key",
            "apim-test");
        return client;
    }

    private sealed class StubTransport(
        string? setCookie,
        params HttpStatusCode[] statuses) : HttpMessageHandler
    {
        private int attempts;

        public string? LastCookieSent { get; private set; }

        public List<string?> CookiesSent { get; } = [];

        public List<string?> AuthorizationsSent { get; } = [];

        public List<string?> SubscriptionKeysSent { get; } = [];

        public List<string?> BodiesSent { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastCookieSent = request.Headers.TryGetValues(
                "Cookie",
                out var values)
                ? string.Join("; ", values)
                : null;
            CookiesSent.Add(LastCookieSent);
            AuthorizationsSent.Add(request.Headers.TryGetValues(
                "X-Authorization",
                out var authorization)
                ? string.Join("; ", authorization)
                : null);
            SubscriptionKeysSent.Add(request.Headers.TryGetValues(
                "Ocp-Apim-Subscription-Key",
                out var subscriptionKey)
                ? string.Join("; ", subscriptionKey)
                : null);
            BodiesSent.Add(request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(
                    cancellationToken));

            var statusCode = attempts < statuses.Length
                ? statuses[attempts]
                : HttpStatusCode.OK;
            attempts++;

            var response = new HttpResponseMessage(statusCode);
            if (response.IsSuccessStatusCode && setCookie is not null)
            {
                response.Headers.TryAddWithoutValidation(
                    "Set-Cookie",
                    setCookie);
            }

            return response;
        }
    }
}
