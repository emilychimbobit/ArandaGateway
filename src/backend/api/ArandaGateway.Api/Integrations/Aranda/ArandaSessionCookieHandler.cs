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
        if (sessionCookie.Value is { } cookie)
        {
            request.Headers.Remove("Cookie");
            request.Headers.TryAddWithoutValidation("Cookie", cookie);
        }

        var response = await base.SendAsync(request, cancellationToken);

        CaptureRenewedCookie(response);

        return response;
    }

    private void CaptureRenewedCookie(HttpResponseMessage response)
    {
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
