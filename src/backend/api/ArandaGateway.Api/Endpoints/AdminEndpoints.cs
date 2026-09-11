using System.Security.Cryptography;
using System.Text;
using ArandaGateway.Api.Administration;
using ArandaGateway.Api.Contracts.Administration;
using ArandaGateway.Api.Integrations.Aranda;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace ArandaGateway.Api.Endpoints;

/// <summary>
/// Operaciones de soporte. Permiten reemplazar la sesión de Aranda sin
/// reiniciar ni redesplegar: la cookie caduca por inactividad y un despliegue
/// completo tarda más que su vida útil, así que renovarla por configuración
/// obliga a coordinar el cambio en una ventana de minutos.
/// </summary>
public static class AdminEndpoints
{
    public const string AdminKeyHeaderName = "X-Admin-Key";

    public static IEndpointRouteBuilder MapAdminEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var options = endpoints.ServiceProvider
            .GetRequiredService<IOptions<AdminOptions>>()
            .Value;

        // Sin clave configurada las rutas no existen. El resto de la gateway
        // es anónima, así que publicar sin credencial un endpoint que instala
        // una sesión dejaría a cualquiera con la URL suplantarla.
        if (!options.IsEnabled)
        {
            endpoints.ServiceProvider
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger(typeof(AdminEndpoints))
                .LogInformation(
                    "Operaciones administrativas deshabilitadas: falta configurar Admin:ApiKey.");

            return endpoints;
        }

        var group = endpoints
            .MapGroup("/admin")
            .WithTags("Administración")
            .ExcludeFromDescription();

        group
            .MapPut("/aranda-session", ReemplazarSesionAsync)
            .WithName("ReemplazarSesionAranda")
            .WithSummary("Instala una cookie de sesión de Aranda sin reiniciar")
            .Produces<RespuestaSesionAranda>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized);

        group
            .MapGet("/aranda-session", ObtenerEstadoSesion)
            .WithName("EstadoSesionAranda")
            .WithSummary("Indica si hay sesión de Aranda y cuándo se renovó")
            .Produces<RespuestaSesionAranda>()
            .Produces(StatusCodes.Status401Unauthorized);

        return endpoints;
    }

    private static IResult ReemplazarSesionAsync(
        [FromHeader(Name = AdminKeyHeaderName)] string? adminKey,
        SolicitudSesionAranda request,
        ArandaSessionCookie sessionCookie,
        IOptions<AdminOptions> options,
        ILoggerFactory loggerFactory)
    {
        if (!IsAuthorized(adminKey, options.Value))
        {
            return Results.Unauthorized();
        }

        var cookie = request.Cookie?.Trim();

        if (string.IsNullOrWhiteSpace(cookie) ||
            !cookie.StartsWith(
                $"{ArandaSessionCookie.CookieName}=",
                StringComparison.OrdinalIgnoreCase))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "La cookie no tiene el formato esperado.",
                detail:
                    $"Se espera el par completo, por ejemplo {ArandaSessionCookie.CookieName}=VALOR.");
        }

        sessionCookie.Renew(cookie);

        // El valor nunca se registra: es una credencial de sesión.
        loggerFactory
            .CreateLogger(typeof(AdminEndpoints))
            .LogWarning("Sesión de Aranda reemplazada manualmente.");

        return Results.Ok(BuildStatus(sessionCookie));
    }

    private static IResult ObtenerEstadoSesion(
        [FromHeader(Name = AdminKeyHeaderName)] string? adminKey,
        ArandaSessionCookie sessionCookie,
        IOptions<AdminOptions> options) =>
        IsAuthorized(adminKey, options.Value)
            ? Results.Ok(BuildStatus(sessionCookie))
            : Results.Unauthorized();

    private static RespuestaSesionAranda BuildStatus(
        ArandaSessionCookie sessionCookie) =>
        new(sessionCookie.Value is not null, sessionCookie.RenewedAt);

    /// <summary>
    /// Compara en tiempo constante para que la respuesta no revele cuántos
    /// caracteres de la clave son correctos.
    /// </summary>
    private static bool IsAuthorized(string? provided, AdminOptions options)
    {
        if (string.IsNullOrWhiteSpace(provided) ||
            string.IsNullOrWhiteSpace(options.ApiKey))
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(provided),
            Encoding.UTF8.GetBytes(options.ApiKey));
    }
}
