using ArandaGateway.Api.Integrations.Aranda.Models;
using Microsoft.Extensions.Options;

namespace ArandaGateway.Api.Integrations.Aranda;

/// <summary>
/// Mantiene viva la sesión de Aranda. La sesión usa expiración deslizante, así
/// que caduca por inactividad. Si Aranda rechaza la cookie, el handler la
/// descarta y reintenta sin ella usando X-Authorization. Este latido hace una
/// consulta de solo lectura cada
/// <see cref="ArandaOptions.SessionKeepAliveMinutes"/> minutos, lo que reinicia
/// el contador y renueva la cookie a través de
/// <see cref="ArandaSessionCookieHandler"/>.
///
/// Solo se activa si hay una cookie semilla configurada: sin ella no hay sesión
/// que mantener y las llamadas usan X-Authorization sin cookie.
/// </summary>
public sealed class ArandaSessionKeepAliveService(
    IServiceScopeFactory scopeFactory,
    ArandaSessionCookie sessionCookie,
    IOptions<ArandaOptions> options,
    ILogger<ArandaSessionKeepAliveService> logger) : BackgroundService
{
    private readonly ArandaOptions arandaOptions = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var period = TimeSpan.FromMinutes(
            arandaOptions.SessionKeepAliveMinutes);

        if (period <= TimeSpan.Zero)
        {
            logger.LogInformation(
                "Latido de sesión de Aranda desactivado por configuración.");
            return;
        }

        logger.LogInformation(
            "Latido de sesión de Aranda activo cada {Minutes} minuto(s).",
            arandaOptions.SessionKeepAliveMinutes);

        using var timer = new PeriodicTimer(period);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            // Se evalúa en cada tick y no una sola vez al arrancar: con la
            // sesión por cookie apagada no hay nada que mantener, pero si
            // alguien instala una por PUT /admin/aranda-session el latido debe
            // empezar a sostenerla sin reiniciar el proceso.
            if (sessionCookie.Value is null)
            {
                continue;
            }

            await SendHeartbeatAsync(stoppingToken);
        }
    }

    private async Task SendHeartbeatAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var client = scope.ServiceProvider
                .GetRequiredService<IArandaClient>();

            // La consulta más barata que Aranda acepta: una sola fila y sin
            // efectos secundarios. Lo que importa es tocar la sesión.
            await client.SearchTicketsAsync(
                new()
                {
                    Criteria = [],
                    PageIndex = 0,
                    PageSize = 1,
                    Projects = [new ArandaProjectFilter(arandaOptions.ProjectId)],
                    Repository = 3,
                    Types = [new ArandaItemTypeFilter(4)]
                },
                cancellationToken);

            logger.LogDebug("Latido de sesión de Aranda completado.");
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            // El host se está deteniendo.
        }
        catch (Exception exception)
        {
            // Un latido fallido no debe tumbar el servicio: la siguiente
            // petición real y el próximo latido volverán a intentarlo.
            logger.LogWarning(
                exception,
                "El latido de sesión de Aranda falló incluso después del fallback de autenticación.");
        }
    }
}
