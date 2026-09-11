using System.Net;
using ArandaGateway.Api.Integrations.Aranda;
using Microsoft.Extensions.Logging.Abstractions;

namespace ArandaGateway.Api.Tests.Integrations.Aranda;

public sealed class ArandaRetryHandlerTests
{
    [Fact]
    public async Task RetriesCloudflareChallengeForWrites()
    {
        var transport = new StubTransport(
            Challenge(),
            Ok());
        using var client = CreateClient(transport);

        using var response = await SendAsync(
            client,
            ArandaRetryPolicy.EdgeRejectionsOnly);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, transport.Attempts);
    }

    [Fact]
    public async Task RetriesRateLimitForWrites()
    {
        var transport = new StubTransport(
            new HttpResponseMessage(HttpStatusCode.TooManyRequests),
            Ok());
        using var client = CreateClient(transport);

        using var response = await SendAsync(
            client,
            ArandaRetryPolicy.EdgeRejectionsOnly);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, transport.Attempts);
    }

    /// <summary>
    /// El caso que protege contra tickets duplicados: un 500 pudo ejecutarse en
    /// Aranda, así que una escritura no se repite.
    /// </summary>
    [Fact]
    public async Task DoesNotRetryServerErrorForWrites()
    {
        var transport = new StubTransport(
            new HttpResponseMessage(HttpStatusCode.InternalServerError),
            Ok());
        using var client = CreateClient(transport);

        using var response = await SendAsync(
            client,
            ArandaRetryPolicy.EdgeRejectionsOnly);

        Assert.Equal(
            HttpStatusCode.InternalServerError,
            response.StatusCode);
        Assert.Equal(1, transport.Attempts);
    }

    [Fact]
    public async Task DoesNotRetryTransportFailureForWrites()
    {
        var transport = new StubTransport(new HttpRequestException("caido"));
        using var client = CreateClient(transport);

        await Assert.ThrowsAsync<HttpRequestException>(
            () => SendAsync(client, ArandaRetryPolicy.EdgeRejectionsOnly));

        Assert.Equal(1, transport.Attempts);
    }

    [Fact]
    public async Task RetriesServerErrorForReads()
    {
        var transport = new StubTransport(
            new HttpResponseMessage(HttpStatusCode.BadGateway),
            Ok());
        using var client = CreateClient(transport);

        using var response = await SendAsync(
            client,
            ArandaRetryPolicy.Reads);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, transport.Attempts);
    }

    [Fact]
    public async Task RetriesTransportFailureForReads()
    {
        var transport = new StubTransport(
            new HttpRequestException("caido"),
            Ok());
        using var client = CreateClient(transport);

        using var response = await SendAsync(
            client,
            ArandaRetryPolicy.Reads);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, transport.Attempts);
    }

    /// <summary>
    /// Un 401 por credenciales no se arregla repitiendo: debe llegar tal cual.
    /// </summary>
    [Fact]
    public async Task DoesNotRetryUnauthorized()
    {
        var transport = new StubTransport(
            new HttpResponseMessage(HttpStatusCode.Unauthorized),
            Ok());
        using var client = CreateClient(transport);

        using var response = await SendAsync(
            client,
            ArandaRetryPolicy.Reads);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(1, transport.Attempts);
    }

    [Fact]
    public async Task DoesNotRetryWhenPolicyIsNone()
    {
        var transport = new StubTransport(Challenge(), Ok());
        using var client = CreateClient(transport);

        using var response = await SendAsync(
            client,
            ArandaRetryPolicy.None);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(1, transport.Attempts);
    }

    [Fact]
    public async Task GivesUpAfterThreeAttempts()
    {
        var transport = new StubTransport(
            Challenge(),
            Challenge(),
            Challenge(),
            Ok());
        using var client = CreateClient(transport);

        using var response = await SendAsync(
            client,
            ArandaRetryPolicy.Reads);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(3, transport.Attempts);
    }

    [Fact]
    public async Task ResendsBodyAndHeadersOnRetry()
    {
        var transport = new StubTransport(Challenge(), Ok());
        using var client = CreateClient(transport);

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "https://aranda.example/api/v9/item/")
        {
            Content = new StringContent("""{"subject":"prueba"}""")
        };
        request.Headers.TryAddWithoutValidation("X-Authorization", "Bearer t");
        request.Options.Set(
            ArandaRetryHandler.PolicyKey,
            ArandaRetryPolicy.EdgeRejectionsOnly);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, transport.Attempts);
        Assert.Equal("""{"subject":"prueba"}""", transport.LastBody);
        Assert.Equal("Bearer t", transport.LastAuthorization);
    }

    private static Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        ArandaRetryPolicy policy)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            "https://aranda.example/api/v9/item/")
        {
            Content = new StringContent("{}")
        };
        request.Options.Set(ArandaRetryHandler.PolicyKey, policy);

        return client.SendAsync(request);
    }

    private static HttpResponseMessage Ok() => new(HttpStatusCode.OK);

    private static HttpResponseMessage Challenge()
    {
        var response = new HttpResponseMessage(HttpStatusCode.Forbidden);
        response.Headers.TryAddWithoutValidation("Cf-Mitigated", "challenge");
        return response;
    }

    private static HttpClient CreateClient(StubTransport transport)
    {
        var handler = new ArandaRetryHandler(
            NullLogger<ArandaRetryHandler>.Instance)
        {
            InnerHandler = transport
        };

        return new(handler);
    }

    private sealed class StubTransport : HttpMessageHandler
    {
        private readonly Queue<object> outcomes;

        public StubTransport(params object[] outcomes) =>
            this.outcomes = new(outcomes);

        public int Attempts { get; private set; }

        public string? LastBody { get; private set; }

        public string? LastAuthorization { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Attempts++;

            LastBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            LastAuthorization = request.Headers.TryGetValues(
                "X-Authorization",
                out var values)
                ? string.Join(",", values)
                : null;

            var outcome = outcomes.Count > 0
                ? outcomes.Dequeue()
                : new HttpResponseMessage(HttpStatusCode.OK);

            return outcome switch
            {
                HttpResponseMessage response => response,
                Exception exception => throw exception,
                _ => throw new InvalidOperationException("Resultado no soportado.")
            };
        }
    }
}
