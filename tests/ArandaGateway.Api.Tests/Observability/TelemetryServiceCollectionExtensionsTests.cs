using ArandaGateway.Api.Observability;
using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ArandaGateway.Api.Tests.Observability;

public sealed class TelemetryServiceCollectionExtensionsTests
{
    private const string ConnectionString =
        "InstrumentationKey=00000000-0000-0000-0000-000000000000;" +
        "IngestionEndpoint=https://telemetria.example/";

    /// <summary>
    /// Sin cadena de conexión el SDK se quedaría reintentando envíos que nadie
    /// recibe, así que en local y en las pruebas no debe activarse.
    /// </summary>
    [Fact]
    public void DoesNotRegisterTelemetryWithoutConnectionString()
    {
        var services = Configure(settings: []);

        Assert.DoesNotContain(
            services,
            descriptor => descriptor.ServiceType == typeof(TelemetryClient));
    }

    [Fact]
    public void RegistersTelemetryFromDedicatedKey()
    {
        var services = Configure(new()
        {
            [TelemetryServiceCollectionExtensions.ConnectionStringKey] =
                ConnectionString
        });

        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(TelemetryClient));
    }

    /// <summary>
    /// Es la variable que inyecta Azure App Service al vincular el recurso.
    /// </summary>
    [Fact]
    public void RegistersTelemetryFromAzureEnvironmentVariable()
    {
        var services = Configure(new()
        {
            ["APPLICATIONINSIGHTS_CONNECTION_STRING"] = ConnectionString
        });

        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(TelemetryClient));
    }

    [Fact]
    public void IgnoresBlankConnectionString()
    {
        var services = Configure(new()
        {
            [TelemetryServiceCollectionExtensions.ConnectionStringKey] = "   "
        });

        Assert.DoesNotContain(
            services,
            descriptor => descriptor.ServiceType == typeof(TelemetryClient));
    }

    private static IServiceCollection Configure(
        Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();

        var services = new ServiceCollection();
        services.AddGatewayTelemetry(configuration, "Development");

        return services;
    }
}
