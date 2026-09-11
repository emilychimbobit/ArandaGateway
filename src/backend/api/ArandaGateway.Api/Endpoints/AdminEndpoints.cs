using ArandaGateway.Api.Contracts.Administration;
using ArandaGateway.Api.Integrations.Aranda;

namespace ArandaGateway.Api.Endpoints;

/// <summary>
/// Operaciones de soporte. Permiten reemplazar la sesión de Aranda sin
/// reiniciar ni redesplegar: la cookie caduca por inactividad y un despliegue
/// completo tarda más que su vida útil, así que renovarla por configuración
/// obliga a coordinar el cambio en una ventana de minutos.
///
/// <para>
/// RIESGO ASUMIDO: estas rutas son anónimas, igual que el resto de la gateway,
/// y el App Service responde desde internet. Cualquiera que conozca la URL
/// puede instalar la cookie con la que el gateway opera contra Aranda, o
/// dejarlo inoperativo enviando una inválida. A diferencia del resto de la
/// gateway, que solo lee con una credencial fija, esto cambia con qué
/// credencial actúa el servicio.
/// </para>
///
/// <para>
/// Mitigación pendiente: restringir <c>/admin/*</c> por IP con las reglas de
/// acceso del App Service, o reponer una clave de autorización. Ver
/// docs/deuda-tecnica.md.
/// </para>
/// </summary>
public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints
            .MapGroup("/admin")
            .WithTags("Administración")
            .ExcludeFromDescription();

        group
            .MapPut("/aranda-session", ReemplazarSesion)
            .WithName("ReemplazarSesionAranda")
            .WithSummary("Instala una cookie de sesión de Aranda sin reiniciar")
            .Produces<RespuestaSesionAranda>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group
            .MapGet("/aranda-session", ObtenerEstadoSesion)
            .WithName("EstadoSesionAranda")
            .WithSummary("Indica si hay sesión de Aranda y cuándo se renovó")
            .Produces<RespuestaSesionAranda>();

        return endpoints;
    }

    private static IResult ReemplazarSesion(
        SolicitudSesionAranda request,
        ArandaSessionCookie sessionCookie,
        ILoggerFactory loggerFactory)
    {
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

        // El valor nunca se registra: es una credencial de sesión. Se deja
        // traza de la operación porque, siendo anónima, conviene poder ver
        // cuándo y cuántas veces se reemplazó la sesión.
        loggerFactory
            .CreateLogger(typeof(AdminEndpoints))
            .LogWarning("Sesión de Aranda reemplazada manualmente.");

        return Results.Ok(BuildStatus(sessionCookie));
    }

    private static IResult ObtenerEstadoSesion(
        ArandaSessionCookie sessionCookie) =>
        Results.Ok(BuildStatus(sessionCookie));

    private static RespuestaSesionAranda BuildStatus(
        ArandaSessionCookie sessionCookie) =>
        new(sessionCookie.Value is not null, sessionCookie.RenewedAt);
}
