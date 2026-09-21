using System.Net;
using System.Net.Http.Json;
using ArandaGateway.Api.Contracts.Tickets;
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

    /// <summary>
    /// "clasificacion" es un segmento literal y debe ganarle a
    /// "/api/tickets/{caseNumber}", que lo tomaria por un numero de caso y
    /// respondería 400 por falta de colaborador.
    /// </summary>
    [Fact]
    public async Task ClassificationEndpoint_WinsOverTheCaseNumberRoute()
    {
        using var response = await client.GetAsync(
            "/api/tickets/clasificacion",
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var clasificacion = await response.Content
            .ReadFromJsonAsync<RespuestaClasificacionTicket>(
                CancellationToken.None);
        Assert.Equal("Por categorizar", clasificacion?.Servicio);
        Assert.Equal("Mesa de Ayuda", clasificacion?.Grupo);
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
