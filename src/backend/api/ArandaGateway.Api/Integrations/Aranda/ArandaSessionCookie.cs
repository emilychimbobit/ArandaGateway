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
    private bool enabled;

    public ArandaSessionCookie(IOptions<ArandaOptions> options)
    {
        enabled = options.Value.SessionCookieEnabled;
        current = enabled ? Normalize(options.Value.AuthCookie) : null;
    }

    /// <summary>Cookie a enviar, o <c>null</c> si no hay ninguna conocida.</summary>
    public string? Value => Volatile.Read(ref current);

    /// <summary>
    /// Momento en que Aranda renovó la cookie por última vez. <c>null</c>
    /// mientras solo se haya usado la semilla de configuración.
    /// </summary>
    public DateTimeOffset? RenewedAt { get; private set; }

    /// <summary>
    /// Adopta la cookie que devolvió Aranda. Con la sesión apagada por
    /// <see cref="ArandaOptions.SessionCookieEnabled"/> no hace nada: si
    /// adoptara, un solo <c>Set-Cookie</c> volvería a encender el envío.
    /// </summary>
    public void Renew(string cookie)
    {
        if (!Volatile.Read(ref enabled))
        {
            return;
        }

        Store(cookie);
    }

    /// <summary>
    /// Instala una cookie a mano desde <c>PUT /admin/aranda-session</c>. A
    /// diferencia de <see cref="Renew"/>, funciona con la sesión apagada y la
    /// vuelve a encender: es la vía de soporte para reactivarla en caliente.
    /// </summary>
    public void Install(string cookie)
    {
        if (Normalize(cookie) is null)
        {
            return;
        }

        Volatile.Write(ref enabled, true);
        Store(cookie);
    }

    private void Store(string cookie)
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
