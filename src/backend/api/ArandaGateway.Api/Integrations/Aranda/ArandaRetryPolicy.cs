namespace ArandaGateway.Api.Integrations.Aranda;

/// <summary>
/// Qué fallos puede reintentar una operación. La distinción existe por los
/// duplicados: reintentar una creación solo es seguro si hay certeza de que la
/// petición no llegó a Aranda.
/// </summary>
public enum ArandaRetryPolicy
{
    /// <summary>Sin reintentos.</summary>
    None = 0,

    /// <summary>
    /// Solo rechazos del borde: el desafío de Cloudflare y la limitación por
    /// volumen. Ambos se resuelven antes de llegar a Aranda, así que la
    /// operación no se ejecutó y repetirla no duplica nada. Es la política de
    /// las operaciones que escriben (crear, actualizar, adjuntar).
    /// </summary>
    EdgeRejectionsOnly = 1,

    /// <summary>
    /// Rechazos del borde más errores de servidor y de red. Solo para
    /// operaciones de lectura: ahí repetir es inofensivo aunque la petición
    /// original sí haya llegado a ejecutarse.
    /// </summary>
    Reads = 2
}
