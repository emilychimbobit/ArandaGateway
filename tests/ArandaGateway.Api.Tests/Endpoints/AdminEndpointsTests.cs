using System.Net;
using System.Net.Http.Json;
using ArandaGateway.Api.Contracts.Administration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace ArandaGateway.Api.Tests.Endpoints;

public sealed class AdminEndpointsTests
    : IClassFixture<AdminEndpointsTests.GatewayFactory>
{
    private readonly GatewayFactory factory;

    public AdminEndpointsTests(GatewayFactory factory) =>
        this.factory = factory;

    [Fact]
    public async Task ReplaceSession_InstallsCookieAndReportsStatus()
    {
        using var client = factory.CreateClient();

        using var response = await client.PutAsJsonAsync(
            "/admin/aranda-session",
            new SolicitudSesionAranda(
                "AuthCookieASMS=NUEVA; path=/; HttpOnly"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var status = await response.Content
            .ReadFromJsonAsync<RespuestaSesionAranda>();

        Assert.True(status!.HaySesion);
        Assert.NotNull(status.RenovadaEn);
    }

    [Fact]
    public async Task ReplaceSession_RejectsCookieWithoutExpectedName()
    {
        using var client = factory.CreateClient();

        using var response = await client.PutAsJsonAsync(
            "/admin/aranda-session",
            new SolicitudSesionAranda("cualquier-cosa"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ReplaceSession_RejectsBlankCookie()
    {
        using var client = factory.CreateClient();

        using var response = await client.PutAsJsonAsync(
            "/admin/aranda-session",
            new SolicitudSesionAranda("   "));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// El estado nunca incluye el valor de la cookie: es una credencial y no
    /// debe volver en una respuesta.
    /// </summary>
    [Fact]
    public async Task Status_DoesNotExposeTheCookieValue()
    {
        using var client = factory.CreateClient();

        await client.PutAsJsonAsync(
            "/admin/aranda-session",
            new SolicitudSesionAranda("AuthCookieASMS=SECRETA"));

        var body = await client.GetStringAsync("/admin/aranda-session");

        Assert.DoesNotContain("SECRETA", body);
    }

    public sealed class GatewayFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder) =>
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["Aranda:BaseUrl"] = "https://aranda.example/",
                        ["Aranda:ApiKey"] = "Bearer test",
                        ["Aranda:AuthCookie"] = null,
                        // El latido saldría a la red durante las pruebas.
                        ["Aranda:SessionKeepAliveMinutes"] = "0"
                    }));
    }
}
