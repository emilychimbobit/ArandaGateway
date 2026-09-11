using OpenTelemetry.Resources;

namespace ArandaGateway.Api.Observability;

public static class TelemetryServiceCollectionExtensions
{
    public const string ConnectionStringKey =
        "ApplicationInsights:ConnectionString";

    /// <summary>
    /// Nombre con el que la gateway aparece en el mapa de aplicaciones.
    /// </summary>
    public const string ServiceName = "aranda-gateway";

    /// <summary>
    /// Registra Application Insights solo si hay cadena de conexión. Sin ella
    /// el SDK arrancaría igual y se quedaría reintentando envíos que nadie
    /// recibe, así que en local y en las pruebas simplemente no se activa.
    ///
    /// La cadena se lee de <c>ApplicationInsights:ConnectionString</c> o de la
    /// variable estándar <c>APPLICATIONINSIGHTS_CONNECTION_STRING</c>, que es
    /// la que inyecta Azure App Service al vincular el recurso.
    ///
    /// Desde la versión 3 el SDK se apoya en OpenTelemetry, así que el nombre
    /// del servicio y el entorno se declaran como atributos de recurso en
    /// lugar de con un <c>ITelemetryInitializer</c>, que ya no existe.
    /// </summary>
    public static IServiceCollection AddGatewayTelemetry(
        this IServiceCollection services,
        IConfiguration configuration,
        string environmentName)
    {
        var connectionString =
            configuration[ConnectionStringKey] ??
            configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return services;
        }

        services.AddApplicationInsightsTelemetry(options =>
            options.ConnectionString = connectionString);

        services
            .AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(ServiceName)
                // Separa dev de producción cuando ambos comparten recurso.
                .AddAttributes(
                [
                    new("deployment.environment", environmentName)
                ]));

        return services;
    }
}
