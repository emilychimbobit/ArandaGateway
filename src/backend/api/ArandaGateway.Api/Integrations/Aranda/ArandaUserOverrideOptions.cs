using ArandaGateway.Api.Integrations.Aranda.Models;

namespace ArandaGateway.Api.Integrations.Aranda;

/// <summary>
/// PARCHE TEMPORAL. Mientras la API de usuarios de Aranda no esté
/// disponible, la resolución del colaborador (<c>GetUserByUsernameAsync</c>)
/// se reemplaza por un usuario fijo por dominio: uno para el inventario
/// (CMDB/equipos) y otro para tickets.
///
/// Para volver a la validación real basta con poner
/// <c>Aranda:UserOverride:Enabled</c> en <c>false</c> (o eliminar la sección
/// completa de <c>appsettings.json</c>). El código de los servicios vuelve a
/// consultar Aranda sin cambios adicionales. Al retirar el parche de forma
/// definitiva se puede borrar este archivo, la propiedad
/// <see cref="ArandaOptions.UserOverride"/> y los bloques marcados con
/// "PARCHE TEMPORAL" en EquipoService y TicketService.
/// </summary>
public sealed class ArandaUserOverrideOptions
{
    /// <summary>
    /// Interruptor único del parche. En <c>false</c> se ignora todo lo demás
    /// y se usa la API de usuarios de Aranda.
    /// </summary>
    public bool Enabled { get; init; }

    /// <summary>Usuario fijo para la consulta de equipos (CMDB).</summary>
    public ArandaUserOverride? Equipos { get; init; }

    /// <summary>Usuario fijo para las operaciones de tickets.</summary>
    public ArandaUserOverride? Tickets { get; init; }
}

/// <summary>
/// PARCHE TEMPORAL. Datos mínimos del usuario de Aranda que consumen los
/// servicios: identificador para las consultas y nombre de usuario para la
/// verificación de propiedad de tickets.
/// </summary>
public sealed class ArandaUserOverride
{
    public long Id { get; init; }

    public string UserName { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string? Email { get; init; }

    public bool IsUsable => Id > 0 && !string.IsNullOrWhiteSpace(UserName);

    public ArandaUser ToArandaUser() =>
        new()
        {
            Id = Id,
            UserName = UserName,
            Name = string.IsNullOrWhiteSpace(Name) ? UserName : Name,
            Email = Email,
            IsActive = true
        };
}
