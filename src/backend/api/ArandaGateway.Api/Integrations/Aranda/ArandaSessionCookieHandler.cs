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

        sessionCookie.Renew(renewed);
        logger.LogDebug("Sesión de Aranda renovada desde Set-Cookie.");
    }
}
