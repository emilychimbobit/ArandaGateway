namespace ArandaGateway.Api.Contracts.Administration;

/// <summary>
/// Cookie de sesión de Aranda a instalar en caliente. Se espera el par
/// completo, con el prefijo: <c>AuthCookieASMS=...</c>.
/// </summary>
public sealed record SolicitudSesionAranda(string Cookie);

/// <summary>
/// Estado de la sesión. Nunca incluye el valor de la cookie: para eso está
/// <see cref="RespuestaValorSesionAranda"/>, en una ruta aparte.
/// </summary>
public sealed record RespuestaSesionAranda(
    bool HaySesion,
    DateTimeOffset? RenovadaEn);

/// <summary>
/// Valor de la cookie viva. Aranda la renueva en cada respuesta y solo existe
/// en la memoria del proceso, así que un reinicio la pierde de forma
/// irrecuperable: esto permite leerla y reinstalarla después de un despliegue.
///
/// <para>
/// RIESGO ASUMIDO: devuelve una credencial de sesión por una ruta anónima
/// expuesta a internet. Ver docs/deuda-tecnica.md.
/// </para>
/// </summary>
public sealed record RespuestaValorSesionAranda(
    string Cookie,
    DateTimeOffset? RenovadaEn);
