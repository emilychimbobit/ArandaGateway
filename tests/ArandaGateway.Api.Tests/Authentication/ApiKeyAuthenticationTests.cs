using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace ArandaGateway.Api.Tests.Authentication;

public sealed class ApiKeyAuthenticationTests
    : IClassFixture<ApiKeyAuthenticationTests.GatewayFactory>
{
    private const string ApiKey = "gateway-test-api-key";
    private readonly HttpClient client;

    public ApiKeyAuthenticationTests(GatewayFactory factory)
    {
        client = factory.CreateClient();
    }

    [Fact]
    public async Task TicketEndpoint_RejectsMissingApiKey()
    {
        using var response = await client.GetAsync(
            "/api/tickets/154",
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TicketEndpoint_RejectsInvalidApiKey()
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/api/tickets/154");
        request.Headers.Add("X-Api-Key", "invalid");

        using var response = await client.SendAsync(
            request,
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TicketEndpoint_AcceptsValidApiKey()
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/api/tickets/154");
        request.Headers.Add("X-Api-Key", ApiKey);

        using var response = await client.SendAsync(
            request,
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task HealthEndpoint_RemainsAnonymous()
    {
        using var response = await client.GetAsync(
            "/health",
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    public sealed class GatewayFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(
            IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["Gateway:ApiKey"] = ApiKey,
                        ["Aranda:BaseUrl"] =
                            "https://aranda.example/",
                        ["Aranda:ApiKey"] = "Bearer test",
                        ["Aranda:ProjectId"] = "1",
                        ["Aranda:AuthorId"] = "2"
                    });
            });
        }
    }
}
