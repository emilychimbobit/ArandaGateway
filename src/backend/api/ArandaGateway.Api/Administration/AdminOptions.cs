namespace ArandaGateway.Api.Administration;

/// <summary>
/// Configuración de las operaciones administrativas del gateway. Hoy solo
/// habilitan la inyección en caliente de la sesión de Aranda.
/// </summary>
public sealed class AdminOptions
{
    public const string SectionName = "Admin";

    /// <summary>
    /// Clave que autoriza las operaciones administrativas, esperada en el
    /// encabezado <c>X-Admin-Key</c>.
    ///
    /// Sin ella las rutas administrativas no se publican: el resto de la
    /// gateway es anónima por diseño, así que un endpoint que acepta una
    /// credencial no puede quedar abierto por olvido de configuración.
    /// </summary>
    public string? ApiKey { get; init; }

    public bool IsEnabled => !string.IsNullOrWhiteSpace(ApiKey);
}
