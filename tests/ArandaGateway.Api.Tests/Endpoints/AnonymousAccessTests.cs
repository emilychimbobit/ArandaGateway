using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace ArandaGateway.Api.Tests.Endpoints;

public sealed class AnonymousAccessTests
    : IClassFixture<AnonymousAccessTests.GatewayFactory>
{
    private readonly HttpClient client;

    public AnonymousAccessTests(GatewayFactory factory)
    {
        client = factory.CreateClient();
    }

    [Fact]
    public async Task TicketEndpoint_AllowsAnonymousRequests()
    {
        using var response = await client.GetAsync(
            "/api/tickets/154",
            CancellationToken.None);

        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task EquiposEndpoint_AllowsAnonymousRequests()
    {
        using var response = await client.GetAsync(
            "/api/equipos",
            CancellationToken.None);

        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
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
