using System.Net;

namespace ArandaGateway.Api.Integrations.Aranda;

/// <summary>
/// Reintenta las salidas hacia Aranda que fallaron por causas ajenas a la
/// operación. El caso que lo motiva es el desafío de Cloudflare delante de
/// Aranda: aparece de forma intermitente, incluso para peticiones idénticas que
/// funcionaron un minuto antes, y se resuelve en el borde sin llegar a Aranda.
///
/// Qué se reintenta lo decide la <see cref="ArandaRetryPolicy"/> que marca cada
/// operación: las escrituras solo repiten ante rechazos del borde, donde hay
/// certeza de que Aranda no ejecutó nada.
/// </summary>
public sealed class ArandaRetryHandler(
    ILogger<ArandaRetryHandler> logger) : DelegatingHandler
{
    public static readonly HttpRequestOptionsKey<ArandaRetryPolicy> PolicyKey =
        new("ArandaRetryPolicy");

    /// <summary>
    /// Esperas entre intentos. Cortas a propósito: el tiempo total sigue
    /// acotado por <see cref="ArandaOptions.TimeoutSeconds"/>.
    /// </summary>
    private static readonly TimeSpan[] Backoff =
    [
        TimeSpan.FromMilliseconds(500),
        TimeSpan.FromMilliseconds(1500)
    ];

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var policy = request.Options.TryGetValue(PolicyKey, out var configured)
            ? configured
            : ArandaRetryPolicy.None;

        if (policy == ArandaRetryPolicy.None)
        {
            return await base.SendAsync(request, cancellationToken);
        }

        // Una petición ya enviada no se puede reenviar, y su contenido ya se
        // consumió: hay que guardarlo en memoria para poder clonarla.
        if (request.Content is not null)
        {
            await request.Content.LoadIntoBufferAsync(cancellationToken);
        }

        for (var attempt = 0; ; attempt++)
        {
            var isLastAttempt = attempt >= Backoff.Length;
            using var attemptRequest = await CloneAsync(
                request,
                cancellationToken);

            HttpResponseMessage response;
            try
            {
                response = await base.SendAsync(
                    attemptRequest,
                    cancellationToken);
            }
            catch (Exception exception) when (
                !isLastAttempt &&
                policy == ArandaRetryPolicy.Reads &&
                IsTransientFailure(exception, cancellationToken))
            {
                await LogAndWaitAsync(
                    attempt,
                    exception.GetType().Name,
                    cancellationToken);
                continue;
            }

            if (isLastAttempt || !ShouldRetry(response, policy))
            {
                return response;
            }

            var reason = DescribeRetryReason(response);
            response.Dispose();

            await LogAndWaitAsync(attempt, reason, cancellationToken);
        }
    }

    private static bool ShouldRetry(
        HttpResponseMessage response,
        ArandaRetryPolicy policy)
    {
        if (IsEdgeRejection(response))
        {
            return true;
        }

        // Un 5xx pudo ejecutarse en Aranda: repetir una escritura ahí
        // arriesgaria un duplicado, asi que solo se reintenta en lecturas.
        return policy == ArandaRetryPolicy.Reads &&
            (int)response.StatusCode >= 500;
    }

    /// <summary>
    /// Rechazo resuelto en el borde, sin llegar a Aranda: el desafío de
    /// Cloudflare (que se identifica por <c>Cf-Mitigated</c>) y la limitación
    /// por volumen.
    /// </summary>
    private static bool IsEdgeRejection(HttpResponseMessage response) =>
        response.StatusCode == HttpStatusCode.TooManyRequests ||
        (response.StatusCode == HttpStatusCode.Forbidden &&
            response.Headers.Contains("Cf-Mitigated"));

    private static bool IsTransientFailure(
        Exception exception,
        CancellationToken cancellationToken) =>
        exception switch
        {
            HttpRequestException => true,
            // Un timeout de HttpClient llega como cancelación sin que el
            // token la haya pedido.
            TaskCanceledException or OperationCanceledException =>
                !cancellationToken.IsCancellationRequested,
            _ => false
        };

    private static string DescribeRetryReason(HttpResponseMessage response) =>
        IsEdgeRejection(response)
            ? $"rechazo en el borde ({(int)response.StatusCode})"
            : $"error de servidor ({(int)response.StatusCode})";

    private async Task LogAndWaitAsync(
        int attempt,
        string reason,
        CancellationToken cancellationToken)
    {
        var delay = Backoff[attempt];

        logger.LogWarning(
            "Reintentando la llamada a Aranda tras {Reason}. Intento {Attempt} de {Total}, espera {Delay}ms.",
            reason,
            attempt + 1,
            Backoff.Length + 1,
            delay.TotalMilliseconds);

        await Task.Delay(delay, cancellationToken);
    }

    private static async Task<HttpRequestMessage> CloneAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri)
        {
            Version = request.Version,
            VersionPolicy = request.VersionPolicy
        };

        if (request.Content is not null)
        {
            var body = await request.Content.ReadAsByteArrayAsync(
                cancellationToken);
            var content = new ByteArrayContent(body);

            foreach (var header in request.Content.Headers)
            {
                content.Headers.TryAddWithoutValidation(
                    header.Key,
                    header.Value);
            }

            clone.Content = content;
        }

        foreach (var header in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        foreach (var option in request.Options)
        {
            clone.Options.Set(
                new HttpRequestOptionsKey<object?>(option.Key),
                option.Value);
        }

        return clone;
    }
}
