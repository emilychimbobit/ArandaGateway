using System.Net;
using System.Net.Http.Json;
using ArandaGateway.Api.Contracts.Administration;
using ArandaGateway.Api.Endpoints;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace ArandaGateway.Api.Tests.Endpoints;

public sealed class AdminEndpointsTests
{
    private const string AdminKey = "clave-de-soporte";

    [Fact]
    public async Task ReplaceSession_RejectsRequestWithoutKey()
    {
        using var factory = CreateFactory(AdminKey);
        using var client = factory.CreateClient();

        using var response = await client.PutAsJsonAsync(
            "/admin/aranda-session",
            new SolicitudSesionAranda("AuthCookieASMS=ABC"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ReplaceSession_RejectsWrongKey()
    {
        using var factory = CreateFactory(AdminKey);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(
            AdminEndpoints.AdminKeyHeaderName,
            "clave-equivocada");

        using var response = await client.PutAsJsonAsync(
            "/admin/aranda-session",
            new SolicitudSesionAranda("AuthCookieASMS=ABC"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Una clave que es prefijo de la correcta no debe pasar: si pasara, la
    /// comparación estaría truncando en lugar de exigir igualdad.
    /// </summary>
    [Fact]
    public async Task ReplaceSession_RejectsKeyPrefix()
    {
        using var factory = CreateFactory(AdminKey);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(
            AdminEndpoints.AdminKeyHeaderName,
            AdminKey[..5]);

        using var response = await client.PutAsJsonAsync(
            "/admin/aranda-session",
            new SolicitudSesionAranda("AuthCookieASMS=ABC"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ReplaceSession_InstallsCookieAndReportsStatus()
    {
        using var factory = CreateFactory(AdminKey);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(
            AdminEndpoints.AdminKeyHeaderName,
            AdminKey);

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
        using var factory = CreateFactory(AdminKey);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(
            AdminEndpoints.AdminKeyHeaderName,
            AdminKey);

        using var response = await client.PutAsJsonAsync(
            "/admin/aranda-session",
            new SolicitudSesionAranda("cualquier-cosa"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Status_ReportsNoSessionWhenNothingConfigured()
    {
        using var factory = CreateFactory(AdminKey);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(
            AdminEndpoints.AdminKeyHeaderName,
            AdminKey);

        var status = await client.GetFromJsonAsync<RespuestaSesionAranda>(
            "/admin/aranda-session");

        Assert.False(status!.HaySesion);
        Assert.Null(status.RenovadaEn);
    }

    /// <summary>
    /// Sin clave configurada las rutas no se publican: el resto de la gateway
    /// es anónima y un endpoint abierto permitiría instalar una sesión ajena.
    /// </summary>
    [Fact]
    public async Task Routes_AreNotPublishedWithoutConfiguredKey()
    {
        using var factory = CreateFactory(adminKey: null);
        using var client = factory.CreateClient();

        using var response = await client.PutAsJsonAsync(
            "/admin/aranda-session",
            new SolicitudSesionAranda("AuthCookieASMS=ABC"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static GatewayFactory CreateFactory(string? adminKey) =>
        new(adminKey);

    public sealed class GatewayFactory(string? adminKey)
        : WebApplicationFactory<Program>
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
                        ["Aranda:SessionKeepAliveMinutes"] = "0",
                        ["Admin:ApiKey"] = adminKey
                    }));
    }
}
