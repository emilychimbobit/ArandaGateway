namespace ArandaGateway.Api.Integrations.Aranda;

/// <summary>
/// Adjunta la cookie de sesión vigente a cada salida hacia Aranda y guarda la
/// que Aranda devuelve. Sin esto la sesión caduca por inactividad y todas las
/// operaciones empiezan a responder 401.
/// </summary>
public sealed class ArandaSessionCookieHandler(
    ArandaSessionCookie sessionCookie,
    ILogger<ArandaSessionCookieHandler> logger) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var cookie = sessionCookie.Value;
        if (cookie is not null)
        {
            request.Headers.Remove("Cookie");
            request.Headers.TryAddWithoutValidation("Cookie", cookie);

            // La segunda transmisión debe poder reproducir también los POST
            // multipart si Aranda rechaza la cookie antes de ejecutarlos.
            if (request.Content is not null)
            {
                await request.Content.LoadIntoBufferAsync(
                    cancellationToken);
            }
        }

        var response = await base.SendAsync(request, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized &&
            cookie is not null)
        {
            sessionCookie.Invalidate(cookie);
            response.Dispose();

            logger.LogWarning(
                "Aranda rechazó la cookie de sesión; se reintentará la solicitud sin Cookie y con X-Authorization.");

            using var fallbackRequest = await CloneWithoutCookieAsync(
                request,
                cancellationToken);
            var fallbackResponse = await base.SendAsync(
                fallbackRequest,
                cancellationToken);
            CaptureRenewedCookie(fallbackResponse);
            return fallbackResponse;
        }

        CaptureRenewedCookie(response);

        return response;
    }

    private static async Task<HttpRequestMessage> CloneWithoutCookieAsync(
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
            var content = new ByteArrayContent(
                await request.Content.ReadAsByteArrayAsync(
                    cancellationToken));
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
            if (!string.Equals(
                header.Key,
                "Cookie",
                StringComparison.OrdinalIgnoreCase))
            {
                clone.Headers.TryAddWithoutValidation(
                    header.Key,
                    header.Value);
            }
        }

        foreach (var option in request.Options)
        {
            clone.Options.Set(
                new HttpRequestOptionsKey<object?>(option.Key),
                option.Value);
        }

        return clone;
    }

    private void CaptureRenewedCookie(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
        {
            return;
        }

        if (!response.Headers.TryGetValues("Set-Cookie", out var setCookies))
        {
            return;
        }

        var renewed = setCookies.FirstOrDefault(value =>
            value.StartsWith(
                $"{ArandaSessionCookie.CookieName}=",
                StringComparison.OrdinalIgnoreCase));

        if (renewed is null)
        {
            return;
        }

        // La primera renovación se registra en Information: confirma que el
        // mecanismo funciona contra este entorno. Las siguientes van en Debug
        // para no dejar una línea por petición.
        var isFirstRenewal = sessionCookie.RenewedAt is null;

        sessionCookie.Renew(renewed);

        if (isFirstRenewal)
        {
            logger.LogInformation(
                "Sesión de Aranda renovada desde Set-Cookie; a partir de ahora se usa la cookie que devuelve Aranda.");
            return;
        }

        logger.LogDebug("Sesión de Aranda renovada desde Set-Cookie.");
    }
}
