using Microsoft.Extensions.Options;

namespace ArandaGateway.Api.Integrations.Aranda;

/// <summary>
/// Cookie de sesión vigente de Aranda. Aranda usa expiración deslizante:
/// cada respuesta trae un <c>Set-Cookie</c> con una cookie renovada, y lo que
/// invalida la sesión es la inactividad, no el tiempo transcurrido. Guardar la
/// última cookie recibida y reenviarla mantiene la sesión viva mientras haya
/// tráfico.
///
/// El valor de <c>Aranda:AuthCookie</c> es solo la semilla inicial: sirve para
/// el primer request tras arrancar y luego queda sustituido por las cookies
/// que devuelve Aranda.
/// </summary>
public sealed class ArandaSessionCookie
{
    public const string CookieName = "AuthCookieASMS";

    private string? current;

    public ArandaSessionCookie(IOptions<ArandaOptions> options) =>
        current = Normalize(options.Value.AuthCookie);

    /// <summary>Cookie a enviar, o <c>null</c> si no hay ninguna conocida.</summary>
    public string? Value => Volatile.Read(ref current);

    /// <summary>
    /// Momento en que Aranda renovó la cookie por última vez. <c>null</c>
    /// mientras solo se haya usado la semilla de configuración.
    /// </summary>
    public DateTimeOffset? RenewedAt { get; private set; }

    public void Renew(string cookie)
    {
        var normalized = Normalize(cookie);
        if (normalized is null)
        {
            return;
        }

        Volatile.Write(ref current, normalized);
        RenewedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Deja el valor como <c>nombre=valor</c>, descartando atributos como
    /// <c>path</c> o <c>HttpOnly</c> que no viajan en el encabezado
    /// <c>Cookie</c>.
    /// </summary>
    private static string? Normalize(string? cookie)
    {
        if (string.IsNullOrWhiteSpace(cookie))
        {
            return null;
        }

        var value = cookie.Split(';')[0].Trim();
        return value.Length == 0 ? null : value;
    }
}
