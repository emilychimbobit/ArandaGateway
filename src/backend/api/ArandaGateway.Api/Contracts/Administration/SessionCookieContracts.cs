namespace ArandaGateway.Api.Contracts.Administration;

/// <summary>
/// Cookie de sesión de Aranda a instalar en caliente. Se espera el par
/// completo, con el prefijo: <c>AuthCookieASMS=...</c>.
/// </summary>
public sealed record SolicitudSesionAranda(string Cookie);

/// <summary>
/// Estado de la sesión. Nunca incluye el valor de la cookie: es una credencial
/// y no debe volver en una respuesta.
/// </summary>
public sealed record RespuestaSesionAranda(
    bool HaySesion,
    DateTimeOffset? RenovadaEn);
